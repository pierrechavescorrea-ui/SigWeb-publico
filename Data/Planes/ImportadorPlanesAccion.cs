using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Documentos;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Planes;

// Importa planes de acción desde un Excel ya mapeado (PanelImportarExcel). Una fila = una acción;
// las filas con el mismo código son acciones del mismo plan. Sin columna de código, cada fila es un
// plan nuevo (PA-###). Un código que ya existe en el listado recibe las acciones nuevas (no se
// duplican las que tienen la misma descripción). Las columnas sin destino propio van a Campos.
public static class ImportadorPlanesAccion
{
    public const string Tabla = "planes-accion";

    public sealed record Resultado(int Planes, int PlanesActualizados, int Acciones, List<string> Avisos);

    public static async Task<Resultado> ImportarAsync(AppDbContext db, ImportacionExcel imp, IReadOnlyDictionary<int, string> nombresCampos)
    {
        string T(string[] f, string d) => imp.ColumnaDe(d) is int c ? ImportacionExcel.Celda(f, c).Trim() : "";
        var avisos = new List<string>();

        var torres = await db.ElementosTrazado.AsNoTracking().ToListAsync();
        var comps = await db.Componentes.AsNoTracking().ToListAsync();
        var locs = await db.Catalogos.AsNoTracking().Where(c => c.Tipo == "Localización" && c.Activo).ToListAsync();
        var tiros = await db.Tiros.AsNoTracking().ToListAsync();

        var todos = await db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().Select(p => new { p.Id, p.Codigo, p.Eliminado }).ToListAsync();
        var vigentes = todos.Where(p => !p.Eliminado).GroupBy(p => p.Codigo.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var enPapelera = todos.Where(p => p.Eliminado).Select(p => p.Codigo.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int siguiente = todos.Select(p => Regex.Match(p.Codigo ?? "", @"^PA-(\d+)$")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();

        var carga = new CargaPlanesAccion { NombreArchivo = imp.NombreArchivo, Hoja = imp.Hoja, FilaEncabezados = imp.FilaEncabezados, Fecha = DateTime.Now };
        db.CargasPlanesAccion.Add(carga);
        await db.SaveChangesAsync();

        var planPorCodigo = new Dictionary<string, PlanAccion>(StringComparer.OrdinalIgnoreCase);
        var filasDePlan = new Dictionary<PlanAccion, List<string[]>>();
        int creados = 0, actualizados = 0, acciones = 0, numero = imp.FilaEncabezados;
        foreach (var f in imp.Filas)
        {
            numero++;
            var codigo = T(f, "nat:Codigo");
            if (codigo != "" && enPapelera.Contains(codigo) && !vigentes.ContainsKey(codigo) && !planPorCodigo.ContainsKey(codigo))
            { avisos.Add($"Fila {numero}: el código {codigo} está en la Papelera; la fila no se importó."); continue; }

            PlanAccion? plan = null;
            if (codigo != "" && planPorCodigo.TryGetValue(codigo, out var p0)) plan = p0;
            else if (codigo != "" && vigentes.TryGetValue(codigo, out var idExistente))
            {
                plan = await db.PlanesAccion.FirstAsync(p => p.Id == idExistente);
                planPorCodigo[codigo] = plan;
                actualizados++;
            }
            if (plan is null)
            {
                plan = new PlanAccion
                {
                    Codigo = codigo != "" ? codigo : "PA-" + (++siguiente).ToString("000"),
                    Fecha = FechaTexto.Parsear(T(f, "nat:Fecha")) ?? DateTime.Today,
                    CargaPlanesAccionId = carga.Id, FechaRegistro = DateTime.Now
                };
                db.PlanesAccion.Add(plan);
                planPorCodigo[plan.Codigo] = plan;
                creados++;
            }
            // Datos del plan: el primer valor no vacío de sus filas (en planes existentes, solo si el Excel trae algo).
            void Poner(string destino, Func<string> actual, Action<string> asignar)
            {
                var v = T(f, destino);
                if (v != "" && (plan.Id == 0 && actual() == "" || plan.Id != 0 && !filasDePlan.ContainsKey(plan))) asignar(v);
            }
            Poner("nat:Origen", () => plan.OrigenHallazgo, v => plan.OrigenHallazgo = v);
            Poner("nat:Tipo", () => plan.TipoHallazgo, v => plan.TipoHallazgo = v);
            Poner("nat:Descripcion", () => plan.DescripcionHallazgo, v => plan.DescripcionHallazgo = v);
            Poner("nat:Analisis", () => plan.AnalisisCausa, v => plan.AnalisisCausa = v);
            Poner("nat:Procesos", () => plan.Proceso, v => plan.Proceso = string.Join("\n", Partir(v)));
            if (plan.Id != 0 && !filasDePlan.ContainsKey(plan) && FechaTexto.Parsear(T(f, "nat:Fecha")) is DateTime fp) plan.Fecha = fp;
            if (!filasDePlan.TryGetValue(plan, out var lf)) filasDePlan[plan] = lf = new();
            lf.Add(f);
        }
        await db.SaveChangesAsync();

        // Lugares, acciones y campos de cada plan.
        var idsPlanes = filasDePlan.Keys.Select(p => p.Id).ToList();
        var lugaresYa = await db.LugaresPlan.AsNoTracking().Where(l => idsPlanes.Contains(l.PlanAccionId)).ToListAsync();
        var accionesYa = await db.AccionesPlan.AsNoTracking().Where(a => idsPlanes.Contains(a.PlanAccionId)).ToListAsync();
        var sinLugar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extras = new Dictionary<int, Dictionary<string, string>>();
        var columnasCampo = imp.Destinos.Where(kv => !kv.Value.StartsWith("nat:") && !kv.Value.StartsWith("acc:")).ToList();
        foreach (var (plan, filas) in filasDePlan)
        {
            int orden = accionesYa.Where(a => a.PlanAccionId == plan.Id).Select(a => a.Orden).DefaultIfEmpty(0).Max();
            foreach (var f in filas)
            {
                foreach (var texto in Partir(T(f, "nat:Lugares")))
                {
                    var l = Lugar(texto, torres, comps, locs, tiros);
                    if (l is null) { sinLugar.Add(texto); continue; }
                    l.PlanAccionId = plan.Id;
                    bool repetido = lugaresYa.Any(x => x.PlanAccionId == plan.Id && x.ElementoTrazadoId == l.ElementoTrazadoId && x.ComponenteId == l.ComponenteId && x.ItemCatalogoId == l.ItemCatalogoId && x.TiroId == l.TiroId);
                    if (!repetido) { db.LugaresPlan.Add(l); lugaresYa.Add(l); }
                }
                var desc = T(f, "acc:Descripcion");
                if (desc != "" && !accionesYa.Any(a => a.PlanAccionId == plan.Id && string.Equals(a.Descripcion.Trim(), desc, StringComparison.OrdinalIgnoreCase)))
                {
                    var a = new AccionPlan
                    {
                        PlanAccionId = plan.Id, Orden = ++orden, Descripcion = desc,
                        Tipo = TipoAccion(T(f, "acc:Tipo")), Estado = EstadoAccion(T(f, "acc:Estado")),
                        Fecha = FechaTexto.Parsear(T(f, "acc:Fecha")) ?? plan.Fecha,
                        Producto = T(f, "acc:Producto"), Responsable = string.Join("\n", Partir(T(f, "acc:Responsable")))
                    };
                    db.AccionesPlan.Add(a); accionesYa.Add(a); acciones++;
                }
            }
            if (columnasCampo.Count > 0)
            {
                // Por columna, el primer valor no vacío entre las filas del plan.
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (col, destino) in columnasCampo)
                {
                    var nombre = NombreCampo(imp, col, destino, nombresCampos);
                    if (nombre is null) continue;
                    d[nombre] = filas.Select(f => ImportacionExcel.Celda(f, col).Trim()).FirstOrDefault(v => v != "") ?? "";
                }
                extras[plan.Id] = d;
            }
        }
        carga.PlanesCreados = creados;
        carga.AccionesCreadas = acciones;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        if (extras.Count > 0) await MotorTabla.GuardarExtrasExcelAsync(db, Tabla, extras);
        if (creados == 0 && acciones == 0)
        {
            // Nada nuevo: la carga no queda registrada.
            var c = await db.CargasPlanesAccion.FirstOrDefaultAsync(x => x.Id == carga.Id);
            if (c is not null) { db.CargasPlanesAccion.Remove(c); await db.SaveChangesAsync(); }
        }
        if (sinLugar.Count > 0) avisos.Add("Lugares no encontrados (torre, elemento «torre · elemento», localización o tiro): " + string.Join(", ", sinLugar.Take(15)) + (sinLugar.Count > 15 ? "…" : ""));
        return new Resultado(creados, actualizados, acciones, avisos);
    }

    // "c:{id}" → nombre de ese campo; "nueva" → encabezado del Excel (marcado "(Excel)" si choca con una propia).
    private static string? NombreCampo(ImportacionExcel imp, int col, string destino, IReadOnlyDictionary<int, string> nombresCampos)
    {
        if (destino.StartsWith("c:")) return int.TryParse(destino[2..], out var id) && nombresCampos.TryGetValue(id, out var n) ? n : null;
        var enc = imp.Encabezados[col];
        return Propias.Contains(MigracionDocumentos.Normalizar(enc)) ? enc + " (Excel)" : enc;
    }

    private static readonly HashSet<string> Propias = new() { "codigo", "fecha", "origen", "tipo de hallazgo", "descripcion del hallazgo", "procesos", "lugares", "riesgo", "acciones", "estado del plan", "analisis de causa" };

    private static IEnumerable<string> Partir(string v) =>
        Regex.Split(v ?? "", @"[\n;,]+").Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);

    private static LugarPlan? Lugar(string texto, List<ElementoTrazado> torres, List<Componente> comps, List<ItemCatalogo> locs, List<Tiro> tiros)
    {
        string N(string s) => MigracionDocumentos.Normalizar(s);
        var t = N(texto);
        var partes = Regex.Split(texto, @"\s*[·/|>]\s*");
        if (partes.Length == 2 && torres.FirstOrDefault(x => N(x.Codigo) == N(partes[0])) is { } tr
            && comps.FirstOrDefault(c => c.ElementoTrazadoId == tr.Id && N(c.Nombre) == N(partes[1])) is { } cp)
            return new LugarPlan { ComponenteId = cp.Id };
        if (torres.FirstOrDefault(x => N(x.Codigo) == t) is { } torre) return new LugarPlan { ElementoTrazadoId = torre.Id };
        var sinTiro = Regex.Replace(t, @"^tiro\s+", "");
        if (tiros.FirstOrDefault(x => N(x.Nombre) == sinTiro || N(x.Nombre) == t) is { } tiro) return new LugarPlan { TiroId = tiro.Id };
        if (locs.FirstOrDefault(x => N(x.Nombre) == t) is { } loc) return new LugarPlan { ItemCatalogoId = loc.Id };
        return null;
    }

    private static TipoAccionPlan TipoAccion(string v)
    {
        var n = MigracionDocumentos.Normalizar(v);
        if (n.StartsWith("correccion")) return TipoAccionPlan.Correccion;
        if (n.Contains("preventiva")) return TipoAccionPlan.AccionPreventiva;
        return TipoAccionPlan.AccionCorrectiva;
    }

    private static EstadoAccionPlan EstadoAccion(string v)
    {
        var n = MigracionDocumentos.Normalizar(v);
        if (n.StartsWith("cerrad")) return EstadoAccionPlan.Cerrada;
        if (n.Contains("ejecucion") || n.Contains("proceso")) return EstadoAccionPlan.EnEjecucion;
        return EstadoAccionPlan.Abierta;
    }
}
