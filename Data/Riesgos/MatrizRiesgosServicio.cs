using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Riesgos;

// Reglas de la matriz de riesgos: una sola versión vigente, revisiones que
// nacen como copia de la anterior, y seguimientos que siempre se registran
// contra la fila del riesgo de la versión en la que se hicieron (por eso
// una revisión nueva nunca borra ni mueve la trazabilidad anterior).
public class MatrizRiesgosServicio
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopes;
    public MatrizRiesgosServicio(AppDbContext db, IServiceScopeFactory scopes) { _db = db; _scopes = scopes; }

    public VersionMatrizRiesgos? Vigente() =>
        _db.VersionesMatrizRiesgos.AsNoTracking()
            .FirstOrDefault(v => v.Estado == EstadoVersionMatriz.Vigente);

    public List<VersionMatrizRiesgos> Versiones() =>
        _db.VersionesMatrizRiesgos.AsNoTracking()
            .OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id).ToList();

    // Marca una versión como vigente y pasa la anterior a histórica.
    public async Task MarcarVigenteAsync(int versionId)
    {
        var todas = await _db.VersionesMatrizRiesgos.ToListAsync();
        foreach (var v in todas)
        {
            if (v.Id == versionId) v.Estado = EstadoVersionMatriz.Vigente;
            else if (v.Estado == EstadoVersionMatriz.Vigente) v.Estado = EstadoVersionMatriz.Historica;
        }
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    // Crea una revisión nueva copiando los riesgos y sus alcances desde
    // "desdeVersionId" (normalmente la vigente). Los seguimientos y
    // hallazgos NO se copian: quedan amarrados a la versión en la que se
    // registraron, que es lo que conserva la trazabilidad histórica.
    public async Task<VersionMatrizRiesgos> CrearVersionAsync(VersionMatrizRiesgos datos, int? desdeVersionId)
    {
        var nueva = new VersionMatrizRiesgos
        {
            Codigo = (datos.Codigo ?? "").Trim(),
            Fecha = datos.Fecha,
            ElaboradoPor = (datos.ElaboradoPor ?? "").Trim(),
            RevisadoPor = (datos.RevisadoPor ?? "").Trim(),
            AprobadoPor = (datos.AprobadoPor ?? "").Trim(),
            EstadoDocumento = (datos.EstadoDocumento ?? "").Trim(),
            Etapa = (datos.Etapa ?? "").Trim(),
            HojaOrigen = (datos.HojaOrigen ?? "").Trim(),
            DescripcionCambios = (datos.DescripcionCambios ?? "").Trim(),
            Estado = EstadoVersionMatriz.Borrador,
            VersionAnteriorId = desdeVersionId
        };
        _db.VersionesMatrizRiesgos.Add(nueva);
        await _db.SaveChangesAsync();

        if (desdeVersionId is int origen)
        {
            var riesgos = await _db.Riesgos.AsNoTracking()
                .Where(r => r.VersionMatrizRiesgosId == origen)
                .OrderBy(r => r.Orden).ToListAsync();

            var mapa = new Dictionary<int, Riesgo>();
            foreach (var r in riesgos)
            {
                var copia = new Riesgo
                {
                    VersionMatrizRiesgosId = nueva.Id,
                    Codigo = r.Codigo,
                    Orden = r.Orden,
                    Tipo = r.Tipo,
                    EstadoActual = r.EstadoActual,
                    Categoria = r.Categoria,
                    Area = r.Area,
                    Causa = r.Causa,
                    Descripcion = r.Descripcion,
                    Consecuencia = r.Consecuencia,
                    Probabilidad = r.Probabilidad,
                    Impacto = r.Impacto,
                    Disparadores = r.Disparadores,
                    Estrategia = r.Estrategia,
                    PlanRespuesta = r.PlanRespuesta,
                    ResponsablePlan = r.ResponsablePlan,
                    EstadoPlan = r.EstadoPlan,
                    ComoSeHaceSeguimiento = r.ComoSeHaceSeguimiento,
                    RiesgoAnteriorId = r.Id
                };
                _db.Riesgos.Add(copia);
                mapa[r.Id] = copia;
            }
            await _db.SaveChangesAsync();

            var idsOrigen = riesgos.Select(r => r.Id).ToList();
            var elementos = await _db.ElementosRiesgo.AsNoTracking().Where(e => idsOrigen.Contains(e.RiesgoId)).ToListAsync();
            foreach (var e in elementos)
            {
                if (!mapa.TryGetValue(e.RiesgoId, out var destino)) continue;
                _db.ElementosRiesgo.Add(new ElementoRiesgo { RiesgoId = destino.Id, Tipo = e.Tipo, Orden = e.Orden, Texto = e.Texto });
            }
            var alcances = await _db.AlcancesRiesgo.AsNoTracking().Where(a => idsOrigen.Contains(a.RiesgoId)).ToListAsync();
            foreach (var a in alcances)
            {
                if (!mapa.TryGetValue(a.RiesgoId, out var destino)) continue;
                _db.AlcancesRiesgo.Add(new AlcanceRiesgo
                {
                    RiesgoId = destino.Id,
                    ElementoTrazadoId = a.ElementoTrazadoId,
                    ComponenteId = a.ComponenteId,
                    ItemCatalogoId = a.ItemCatalogoId,
                    TiroId = a.TiroId
                });
            }
            await _db.SaveChangesAsync();
        }

        _db.ChangeTracker.Clear();
        return nueva;
    }

    // Siguiente código de riesgo libre dentro de una versión (R-01, R-02…).
    public string SiguienteCodigo(int versionId)
    {
        var usados = _db.Riesgos.AsNoTracking()
            .Where(r => r.VersionMatrizRiesgosId == versionId)
            .Select(r => r.Codigo).ToList();
        for (var n = 1; n < 1000; n++)
        {
            var c = "R-" + n.ToString("00");
            if (!usados.Contains(c, StringComparer.OrdinalIgnoreCase)) return c;
        }
        return "R-" + Guid.NewGuid().ToString("N")[..4];
    }

    // Cadena completa de un riesgo a lo largo de las versiones (el mismo
    // código), de la más nueva a la más vieja.
    public List<Riesgo> Historial(string codigo) =>
        (from r in _db.Riesgos.AsNoTracking()
         join v in _db.VersionesMatrizRiesgos.AsNoTracking() on r.VersionMatrizRiesgosId equals v.Id
         where r.Codigo == codigo
         orderby v.Fecha descending, v.Id descending
         select r).ToList();

    // Hallazgos que corresponden a una torre: los registrados sobre ella, sobre
    // cualquiera de sus patas o elementos, sobre una localización del catálogo
    // amarrada a ella o sobre el tiro al que pertenece (un hallazgo puede tener
    // varios lugares: LugarHallazgo).
    //
    // Usa su propio DbContext: la hoja de vida lo llama en segundo plano
    // mientras la página sigue consultando el suyo, y dos operaciones a la vez
    // sobre el mismo DbContext tumban el circuito ("A second operation…").
    public async Task<List<HallazgoTorre>> HallazgosDeTorreAsync(int elementoTrazadoId)
    {
        using var scope = _scopes.CreateScope();
        var _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var torre = await _db.ElementosTrazado.AsNoTracking().FirstOrDefaultAsync(t => t.Id == elementoTrazadoId);
        if (torre is null) return new();
        var comps = await _db.Componentes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.ElementoTrazadoId == elementoTrazadoId).ToDictionaryAsync(c => c.Id, c => c.Nombre);
        var idsComp = comps.Keys.ToList();
        var locs = await _db.Catalogos.AsNoTracking()
            .Where(i => i.ElementoTrazadoId == elementoTrazadoId).ToDictionaryAsync(i => i.Id, i => i.Nombre);
        var idsLoc = locs.Keys.ToList();
        var tiroId = torre.TiroId;
        var tiroNombre = tiroId is int t0 ? (await _db.Tiros.AsNoTracking().FirstOrDefaultAsync(t => t.Id == t0))?.Nombre : null;

        var lugares = await _db.LugaresHallazgo.AsNoTracking()
            .Where(l => l.ElementoTrazadoId == elementoTrazadoId
                     || (l.ComponenteId != null && idsComp.Contains(l.ComponenteId.Value))
                     || (l.ItemCatalogoId != null && idsLoc.Contains(l.ItemCatalogoId.Value))
                     || (tiroId != null && l.TiroId == tiroId))
            .ToListAsync();
        var idsH = lugares.Select(l => l.HallazgoRiesgoId).Distinct().ToList();

        var q = from h in _db.HallazgosRiesgo.AsNoTracking()
                join s in _db.SeguimientosRiesgo.AsNoTracking() on h.SeguimientoRiesgoId equals s.Id
                join r in _db.Riesgos.AsNoTracking() on s.RiesgoId equals r.Id
                join v in _db.VersionesMatrizRiesgos.AsNoTracking() on r.VersionMatrizRiesgosId equals v.Id
                where idsH.Contains(h.Id)
                orderby s.Fecha descending, h.Id descending
                select new HallazgoTorre
                {
                    Hallazgo = h,
                    SeguimientoId = s.Id,
                    FechaSeguimiento = s.Fecha,
                    ResponsableSeguimiento = s.Responsable,
                    ObservacionSeguimiento = s.Observacion,
                    Resultado = s.Resultado,
                    RiesgoId = r.Id,
                    CodigoRiesgo = r.Codigo,
                    DescripcionRiesgo = r.Descripcion,
                    TipoRiesgo = r.Tipo,
                    Probabilidad = r.Probabilidad,
                    Impacto = r.Impacto,
                    VersionCodigo = v.Codigo,
                    VersionFecha = v.Fecha
                };
        var lista = await q.ToListAsync();

        var idsSeg = lista.Select(x => x.SeguimientoId).Distinct().ToList();
        var soportes = await _db.ArchivosRiesgo.AsNoTracking()
            .Where(a => a.SeguimientoRiesgoId != null && idsSeg.Contains(a.SeguimientoRiesgoId.Value)).ToListAsync();
        var planes = await _db.PlanesAccion.AsNoTracking()
            .Where(p => p.SeguimientoRiesgoId != null && idsSeg.Contains(p.SeguimientoRiesgoId.Value)).ToListAsync();
        var idsPlan = planes.Select(p => p.Id).ToList();
        var acciones = await _db.AccionesPlan.AsNoTracking().Where(a => idsPlan.Contains(a.PlanAccionId)).ToListAsync();

        string Texto(LugarHallazgo l) =>
            l.ElementoTrazadoId is not null ? "Torre"
            : l.ComponenteId is int c ? (comps.TryGetValue(c, out var n) ? n : "Elemento")
            : l.ItemCatalogoId is int i ? (locs.TryGetValue(i, out var nl) ? nl : "Localización")
            : $"Tiro {tiroNombre}";

        foreach (var x in lista)
        {
            x.DondeTexto = string.Join(", ", lugares.Where(l => l.HallazgoRiesgoId == x.Hallazgo.Id).Select(Texto).Distinct());
            x.Archivos = soportes.Where(a => a.SeguimientoRiesgoId == x.SeguimientoId).ToList();
            x.Planes = planes.Where(p => p.SeguimientoRiesgoId == x.SeguimientoId)
                .Select(p => (p, EscalaAccion.EstadoPlan(acciones.Where(a => a.PlanAccionId == p.Id)))).ToList();
        }
        return lista;
    }

    public class HallazgoTorre
    {
        public HallazgoRiesgo Hallazgo { get; set; } = default!;
        public int SeguimientoId { get; set; }
        public ResultadoSeguimiento Resultado { get; set; }
        public ProbabilidadRiesgo Probabilidad { get; set; }
        public ImpactoRiesgo Impacto { get; set; }
        public List<(PlanAccion Plan, EstadoAccionPlan Estado)> Planes { get; set; } = new();
        public DateTime FechaSeguimiento { get; set; }
        public string ResponsableSeguimiento { get; set; } = "";
        public string ObservacionSeguimiento { get; set; } = "";
        public int RiesgoId { get; set; }
        public string CodigoRiesgo { get; set; } = "";
        public string DescripcionRiesgo { get; set; } = "";
        public TipoRiesgo TipoRiesgo { get; set; }
        public string VersionCodigo { get; set; } = "";
        public DateTime VersionFecha { get; set; }
        public string DondeTexto { get; set; } = "";
        public List<ArchivoRiesgo> Archivos { get; set; } = new();
    }
}
