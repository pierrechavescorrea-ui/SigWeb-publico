using Microsoft.EntityFrameworkCore;
using SigWeb.Components.Layout;
using SigWeb.Data.Planes;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Planes de acción: mismas columnas que la página; adjuntos en las mismas carpetas que su ventanita
// (CarpetasVista): una automática por acción ("Corrección 1", "Acción correctiva 1"…) con los anexos de
// sus productos y las carpetas con nombre del plan. Grupo = tipo de hallazgo.
public sealed class FuentePlanesAccion : IFuenteDossier
{
    public string Clave => "planes-accion";
    public string Nombre => "Planes de acción";
    public string Icono => "plan-accion";
    public const string ClaseAcciones = "Anexos de acciones", ClaseCarpetas = "Carpetas del plan";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var planes = await db.PlanesAccion.AsNoTracking().Where(p => !p.Eliminado).OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).ToListAsync(ct);
        var porId = planes.ToDictionary(p => p.Id);
        var acciones = await db.AccionesPlan.AsNoTracking().Where(a => !a.Eliminado).ToListAsync(ct);
        var accionesDe = acciones.GroupBy(a => a.PlanAccionId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Orden).ThenBy(a => a.Id).ToList());
        var localizaciones = await db.Catalogos.AsNoTracking().Where(c => c.Tipo == "Localización" && c.Activo).ToListAsync(ct);
        var torres = await db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).ToListAsync(ct);
        var componentes = await db.Componentes.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var tiros = await db.Tiros.AsNoTracking().Where(t => !t.Eliminado).ToListAsync(ct);
        var lugares = (await db.LugaresPlan.AsNoTracking().ToListAsync(ct)).GroupBy(l => l.PlanAccionId).ToDictionary(g => g.Key, g => g.ToList());
        var cargas = await db.CargasPlanesAccion.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.NombreArchivo + (string.IsNullOrWhiteSpace(c.Hoja) ? "" : " › " + c.Hoja), ct);
        var versiones = await db.VersionesMatrizRiesgos.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(v => v.Id, ct);
        var riesgos = (await db.Riesgos.IgnoreQueryFilters().AsNoTracking()
                .Select(r => new { r.Id, r.Codigo, r.VersionMatrizRiesgosId, r.Eliminado }).ToListAsync(ct))
            .ToDictionary(r => r.Id, r => $"{r.Codigo} ({(versiones.TryGetValue(r.VersionMatrizRiesgosId, out var v) ? v.Codigo : "?")})"
                + (r.Eliminado || (versiones.TryGetValue(r.VersionMatrizRiesgosId, out var v2) && v2.Eliminado) ? " · en Papelera" : ""));
        var riesgosDe = PlanesAccionServicio.RiesgosDePlanes(db, planes);

        PlanAccion? P(int id) => porId.GetValueOrDefault(id);
        List<AccionPlan> A(int id) => accionesDe.TryGetValue(id, out var l) ? l : new();
        var resumen = new Dictionary<int, string>();   // texto de la columna Adjuntos (se llena con las carpetas)
        var nativas = new[]
        {
            new ColumnaNativa("Codigo", "Código", TipoCampoTabla.Texto, id => P(id)?.Codigo),
            new ColumnaNativa("Fecha", "Fecha", TipoCampoTabla.Fecha, id => P(id)?.Fecha),
            new ColumnaNativa("Origen", "Origen", TipoCampoTabla.Texto, id => P(id)?.OrigenHallazgo),
            new ColumnaNativa("Tipo", "Tipo de hallazgo", TipoCampoTabla.Texto, id => P(id)?.TipoHallazgo),
            new ColumnaNativa("Descripcion", "Descripción del hallazgo", TipoCampoTabla.Texto, id => P(id)?.DescripcionHallazgo),
            new ColumnaNativa("Procesos", "Procesos", TipoCampoTabla.Texto, id => P(id)?.Proceso),
            new ColumnaNativa("Lugares", "Lugares", TipoCampoTabla.Texto, id => lugares.TryGetValue(id, out var l)
                ? string.Join("\n", l.Select(x => SelectorLugares.Texto(x, torres, componentes, localizaciones, tiros))) : null),
            new ColumnaNativa("Riesgo", "Riesgos", TipoCampoTabla.Texto, id => riesgosDe.TryGetValue(id, out var l)
                ? string.Join("\n", l.Select(r => riesgos.GetValueOrDefault(r) ?? "").Where(x => x.Length > 0)) : null),
            new ColumnaNativa("Acciones", "Acciones", TipoCampoTabla.Texto, id => string.Join("\n", A(id).Select(a => $"[{EscalaAccion.Nombre(a.Estado)}] {a.Descripcion}"))),
            new ColumnaNativa("Estado", "Estado del plan", TipoCampoTabla.Texto, id => EscalaAccion.Nombre(EscalaAccion.EstadoPlan(A(id)))),
            new ColumnaNativa("Adjuntos", "Adjuntos", TipoCampoTabla.Texto, id => resumen.GetValueOrDefault(id)),
            new ColumnaNativa("Analisis", "Análisis de causa", TipoCampoTabla.Texto, id => P(id)?.AnalisisCausa, visiblePorDefecto: false),
            new ColumnaNativa("Reporto", "Reportó", TipoCampoTabla.Texto, id => P(id) is { } p
                ? string.Join(" · ", new[] { p.ReportoNombre, p.ReportoCargo, p.ReportoEmpresa }.Where(x => !string.IsNullOrWhiteSpace(x))) : null),
            new ColumnaNativa("Carga", "Carga", TipoCampoTabla.Texto, id => P(id)?.CargaPlanesAccionId is int c && cargas.TryGetValue(c, out var n) ? n : null, visiblePorDefecto: false),
        };

        // Adjuntos (CarpetasVista de la página)
        var anexos = (await db.ArchivosAccionPlan.AsNoTracking().Where(x => !x.Eliminado && x.SeguimientoAccionId == null).ToListAsync(ct))
            .GroupBy(x => x.AccionPlanId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.FechaRegistro).ThenByDescending(x => x.Id).ToList());
        var carpetas = (await db.CarpetasPlan.AsNoTracking().Where(c => !c.Eliminado).ToListAsync(ct))
            .GroupBy(c => c.PlanAccionId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList());
        var archivosCarpeta = (await db.ArchivosCarpetaPlan.AsNoTracking().Where(a => !a.Eliminado).ToListAsync(ct))
            .GroupBy(a => a.CarpetaPlanId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FechaCarga).ThenByDescending(a => a.Id).ToList());

        var elementos = new List<ElementoDossier>();
        foreach (var p in planes)
        {
            var el = new ElementoDossier { Id = p.Id, Grupo = p.TipoHallazgo };
            var cuenta = new Dictionary<TipoAccionPlan, int>();
            var lineas = new List<string>();
            foreach (var a in A(p.Id))
            {
                cuenta[a.Tipo] = cuenta.TryGetValue(a.Tipo, out var n) ? n + 1 : 1;
                var carpeta = $"{EscalaAccion.Nombre(a.Tipo)} {cuenta[a.Tipo]}";
                if (!anexos.TryGetValue(a.Id, out var l) || l.Count == 0) continue;
                foreach (var x in l) el.Archivos.Add(new ArchivoDossier(ClaseAcciones, x.NombreOriginal, x.RutaArchivo, Carpeta: carpeta));
                lineas.Add($"{carpeta} ({l.Count})");
            }
            if (carpetas.TryGetValue(p.Id, out var cs))
                foreach (var c in cs)
                {
                    if (!archivosCarpeta.TryGetValue(c.Id, out var l) || l.Count == 0) continue;
                    foreach (var x in l) el.Archivos.Add(new ArchivoDossier(ClaseCarpetas, x.NombreOriginal, x.RutaArchivo, Carpeta: c.Nombre));
                    lineas.Add($"{c.Nombre} ({l.Count})");
                }
            resumen[p.Id] = string.Join("\n", lineas);
            elementos.Add(el);
        }
        var motor = await MotorTabla.CargarLecturaAsync(db, ImportadorPlanesAccion.Tabla, nativas, planes.Select(p => (p.Id, p.Codigo)));

        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Codigo", NombreGrupo = "Tipo de hallazgo",
            Clases = new() { ClaseAcciones, ClaseCarpetas }
        };
    }
}
