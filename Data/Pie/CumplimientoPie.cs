using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Pie;

// % de cumplimiento de UNA torre respecto a la Matriz PIE.
// Misma regla que MatrizPie.razor (PctTorre): promedio del % de estado de todas las celdas
// de las actividades de la matriz que cuelgan de la torre (su tiro, su hoja de vida y sus patas);
// las celdas N/A (registro no completado) no entran en el promedio. null = ninguna celda aplica.
// Si se cambia la regla en MatrizPie, cambiarla también aquí.
public static class CumplimientoPie
{
    public sealed record Resultado(decimal? Pct, int Aplican, int Total);

    public static Resultado DeTorre(AppDbContext db, int elementoTrazadoId)
    {
        var actividades = db.Actividades.AsNoTracking().Where(a => a.Activo && a.EnMatrizPie).ToList();
        if (actividades.Count == 0) return new(null, 0, 0);
        var idsAct = actividades.Select(a => a.Id).ToList();
        var parametros = db.ParametrosActividad.AsNoTracking()
            .Where(p => p.Activo && idsAct.Contains(p.ActividadId)).ToList()
            .GroupBy(p => p.ActividadId).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());
        var pctEstado = db.EstadosParametro.AsNoTracking().Where(e => e.Activo).ToDictionary(e => e.Id, e => e.Porcentaje);

        var comps = db.Componentes.AsNoTracking()
            .Where(c => !c.Eliminado && c.ElementoTrazadoId == elementoTrazadoId).ToList();
        var hojaVida = comps.FirstOrDefault(c => c.EsHojaDeVida)?.Id;
        var patas = comps.Where(c => !c.EsHojaDeVida).Select(c => c.Id).ToList();
        var actsPorComp = db.ComponenteActividades.AsNoTracking().Where(x => patas.Contains(x.ComponenteId)).ToList()
            .GroupBy(x => x.ComponenteId).ToDictionary(g => g.Key, g => g.Select(x => x.ActividadId).ToHashSet());
        bool Aplica(int comp, int act) => !actsPorComp.TryGetValue(comp, out var s) || s.Count == 0 || s.Contains(act);

        var tiroId = db.ElementosTrazado.AsNoTracking().Where(e => e.Id == elementoTrazadoId).Select(e => e.TiroId).FirstOrDefault();
        if (tiroId is int t && !db.Tiros.AsNoTracking().Any(x => x.Id == t && !x.Eliminado)) tiroId = null;

        var idsComp = patas.ToList();
        if (hojaVida is int hv) idsComp.Add(hv);
        var registros = db.RegistrosParametro.AsNoTracking().Where(r => idsComp.Contains(r.ComponenteId)).ToList()
            .GroupBy(r => (r.ComponenteId, r.ParametroActividadId)).ToDictionary(g => g.Key, g => g.First());
        var registrosTiro = tiroId is int tid
            ? db.RegistrosParametroTiro.AsNoTracking().Where(r => r.TiroId == tid).ToList()
                .GroupBy(r => r.ParametroActividadId).ToDictionary(g => g.Key, g => g.First())
            : new Dictionary<int, RegistroParametroTiro>();

        decimal? Pct(bool completado, int? estado) =>
            !completado ? null : (estado is int e && pctEstado.TryGetValue(e, out var p) ? p : 0);

        var celdas = new List<decimal?>();
        foreach (var a in actividades)
        {
            if (!parametros.TryGetValue(a.Id, out var ps)) continue;
            switch (a.Ambito)
            {
                case AmbitoActividad.Torre:
                    if (hojaVida is int c)
                        foreach (var p in ps)
                            celdas.Add(registros.TryGetValue((c, p), out var r) ? Pct(r.Completado, r.EstadoParametroId) : null);
                    break;
                case AmbitoActividad.Pata:
                    foreach (var pa in patas.Where(pa => Aplica(pa, a.Id)))
                        foreach (var p in ps)
                            celdas.Add(registros.TryGetValue((pa, p), out var r) ? Pct(r.Completado, r.EstadoParametroId) : null);
                    break;
                default:
                    if (tiroId is not null)
                        foreach (var p in ps)
                            celdas.Add(registrosTiro.TryGetValue(p, out var rt) ? Pct(rt.Completado, rt.EstadoParametroId) : null);
                    break;
            }
        }
        var aplican = celdas.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return new(aplican.Count == 0 ? null : aplican.Average(), aplican.Count, celdas.Count);
    }

    // % total de la Matriz PIE: misma regla que MatrizPie.TotalGeneral (sin filtros): promedio del % de
    // estado de todas las celdas de todas las actividades de la matriz (torres y patas de las
    // localizaciones activas, y todos los tiros); N/A no entra. Es el estado ACTUAL de la matriz
    // (los registros no guardan historia): para dejar el valor de un mes, se congela en Indicadores.
    // Si se cambia la regla en MatrizPie, cambiarla también aquí.
    public static Resultado Total(AppDbContext db) => Resumir(Celdas(db));

    // Una celda de la matriz: su % (null = N/A) y sus fechas (registro y facturado).
    public sealed record Celda(decimal? Pct, DateTime? Fecha, DateTime? Facturado);

    public static Resultado Resumir(IEnumerable<Celda> celdas)
    {
        var l = celdas.ToList();
        var aplican = l.Where(x => x.Pct.HasValue).Select(x => x.Pct!.Value).ToList();
        return new(aplican.Count == 0 ? null : aplican.Average(), aplican.Count, l.Count);
    }

    // Todas las celdas de la matriz (misma selección que Total).
    public static List<Celda> Celdas(AppDbContext db)
    {
        var actividades = db.Actividades.AsNoTracking().Where(a => a.Activo && a.EnMatrizPie).ToList();
        if (actividades.Count == 0) return new List<Celda>();
        var idsAct = actividades.Select(a => a.Id).ToList();
        var parametros = db.ParametrosActividad.AsNoTracking()
            .Where(p => p.Activo && idsAct.Contains(p.ActividadId)).ToList()
            .GroupBy(p => p.ActividadId).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());
        var pctEstado = db.EstadosParametro.AsNoTracking().Where(e => e.Activo).ToDictionary(e => e.Id, e => e.Porcentaje);

        var idsElem = db.Catalogos.AsNoTracking().Where(c => c.Tipo == "Localización" && c.Activo && c.ElementoTrazadoId != null)
            .Select(c => c.ElementoTrazadoId!.Value).ToList();
        var comps = db.Componentes.AsNoTracking().Where(c => !c.Eliminado && idsElem.Contains(c.ElementoTrazadoId)).ToList();
        // Una hoja de vida por torre (la primera), como ComponentePorElemento en MatrizPie.
        var hojas = comps.Where(c => c.EsHojaDeVida).GroupBy(c => c.ElementoTrazadoId).Select(g => g.First().Id).ToList();
        // Las localizaciones pueden repetir torre: cada fila cuenta (igual que la matriz).
        var hojaPorElem = comps.Where(c => c.EsHojaDeVida).GroupBy(c => c.ElementoTrazadoId).ToDictionary(g => g.Key, g => g.First().Id);
        var otros = comps.Where(c => !c.EsHojaDeVida).ToList();
        var idsOtros = otros.Select(c => c.Id).ToList();
        var actsPorComp = db.ComponenteActividades.AsNoTracking().Where(x => idsOtros.Contains(x.ComponenteId)).ToList()
            .GroupBy(x => x.ComponenteId).ToDictionary(g => g.Key, g => g.Select(x => x.ActividadId).ToHashSet());
        bool Aplica(int comp, int act) => !actsPorComp.TryGetValue(comp, out var s) || s.Count == 0 || s.Contains(act);
        var actsPata = actividades.Where(a => a.Ambito == AmbitoActividad.Pata).Select(a => a.Id).ToList();
        var patasPorElem = otros.Where(c => actsPata.Any(a => Aplica(c.Id, a))).GroupBy(c => c.ElementoTrazadoId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());
        var tiros = db.Tiros.AsNoTracking().Where(t => !t.Eliminado).Select(t => t.Id).ToList();

        var idsComp = hojas.Concat(patasPorElem.Values.SelectMany(l => l)).ToList();
        var registros = db.RegistrosParametro.AsNoTracking().Where(r => idsComp.Contains(r.ComponenteId)).ToList()
            .GroupBy(r => (r.ComponenteId, r.ParametroActividadId)).ToDictionary(g => g.Key, g => g.First());
        var registrosTiro = db.RegistrosParametroTiro.AsNoTracking().Where(r => tiros.Contains(r.TiroId)).ToList()
            .GroupBy(r => (r.TiroId, r.ParametroActividadId)).ToDictionary(g => g.Key, g => g.First());

        decimal? Pct(bool completado, int? estado) =>
            !completado ? null : (estado is int e && pctEstado.TryGetValue(e, out var p) ? p : 0);

        var celdas = new List<Celda>();
        Celda De(bool completado, int? estado, DateTime? fecha, DateTime? fac) =>
            new(Pct(completado, estado), completado ? fecha : null, completado ? fac : null);
        static Celda Nada() => new(null, null, null);
        foreach (var a in actividades)
        {
            if (!parametros.TryGetValue(a.Id, out var ps)) continue;
            switch (a.Ambito)
            {
                case AmbitoActividad.Torre:
                    foreach (var el in idsElem)
                        if (hojaPorElem.TryGetValue(el, out var c))
                            foreach (var p in ps)
                                celdas.Add(registros.TryGetValue((c, p), out var r) ? De(r.Completado, r.EstadoParametroId, r.Fecha, r.FechaFacturado) : Nada());
                    break;
                case AmbitoActividad.Pata:
                    foreach (var el in idsElem)
                        if (patasPorElem.TryGetValue(el, out var pl))
                            foreach (var pa in pl.Where(pa => Aplica(pa, a.Id)))
                                foreach (var p in ps)
                                    celdas.Add(registros.TryGetValue((pa, p), out var r) ? De(r.Completado, r.EstadoParametroId, r.Fecha, r.FechaFacturado) : Nada());
                    break;
                default:
                    foreach (var t in tiros)
                        foreach (var p in ps)
                            celdas.Add(registrosTiro.TryGetValue((t, p), out var rt) ? De(rt.Completado, rt.EstadoParametroId, rt.Fecha, rt.FechaFacturado) : Nada());
                    break;
            }
        }
        return celdas;
    }

    // Una torre (fila del catálogo Localización) con su % y sus registros pendientes: los que aplican y
    // no llegan al 100 % (su estado pesa menos de 100 o no tiene estado).
    public sealed record TorrePie(int LocalizacionId, string Nombre, int? ElementoTrazadoId, decimal? Pct, int Aplican, int Pendientes);

    // % y pendientes de cada torre de la Matriz PIE. Misma regla que MatrizPie.PctTorre / CeldasTorre
    // (su tiro, su hoja de vida y sus patas). Si se cambia la regla en MatrizPie, cambiarla también aquí.
    public static List<TorrePie> PorTorre(AppDbContext db)
    {
        var actividades = db.Actividades.AsNoTracking().Where(a => a.Activo && a.EnMatrizPie).ToList();
        var filas = db.Catalogos.AsNoTracking().Where(c => c.Tipo == "Localización" && c.Activo).ToList();
        if (actividades.Count == 0 || filas.Count == 0) return new List<TorrePie>();
        var idsAct = actividades.Select(a => a.Id).ToList();
        var parametros = db.ParametrosActividad.AsNoTracking()
            .Where(p => p.Activo && idsAct.Contains(p.ActividadId)).ToList()
            .GroupBy(p => p.ActividadId).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());
        var pctEstado = db.EstadosParametro.AsNoTracking().Where(e => e.Activo).ToDictionary(e => e.Id, e => e.Porcentaje);

        var idsElem = filas.Where(f => f.ElementoTrazadoId != null).Select(f => f.ElementoTrazadoId!.Value).Distinct().ToList();
        var comps = db.Componentes.AsNoTracking().Where(c => !c.Eliminado && idsElem.Contains(c.ElementoTrazadoId)).ToList();
        var hojaPorElem = comps.Where(c => c.EsHojaDeVida).GroupBy(c => c.ElementoTrazadoId).ToDictionary(g => g.Key, g => g.First().Id);
        var otros = comps.Where(c => !c.EsHojaDeVida).ToList();
        var idsOtros = otros.Select(c => c.Id).ToList();
        var actsPorComp = db.ComponenteActividades.AsNoTracking().Where(x => idsOtros.Contains(x.ComponenteId)).ToList()
            .GroupBy(x => x.ComponenteId).ToDictionary(g => g.Key, g => g.Select(x => x.ActividadId).ToHashSet());
        bool Aplica(int comp, int act) => !actsPorComp.TryGetValue(comp, out var s) || s.Count == 0 || s.Contains(act);
        var actsPata = actividades.Where(a => a.Ambito == AmbitoActividad.Pata).Select(a => a.Id).ToList();
        var patasPorElem = otros.Where(c => actsPata.Any(a => Aplica(c.Id, a))).GroupBy(c => c.ElementoTrazadoId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());
        var tiros = db.Tiros.AsNoTracking().Where(t => !t.Eliminado).Select(t => t.Id).ToHashSet();
        var tiroPorElem = db.ElementosTrazado.AsNoTracking().Where(e => e.TiroId != null && idsElem.Contains(e.Id)).ToList()
            .Where(e => tiros.Contains(e.TiroId!.Value)).ToDictionary(e => e.Id, e => e.TiroId!.Value);

        var idsComp = hojaPorElem.Values.Concat(patasPorElem.Values.SelectMany(l => l)).ToList();
        var registros = db.RegistrosParametro.AsNoTracking().Where(r => idsComp.Contains(r.ComponenteId)).ToList()
            .GroupBy(r => (r.ComponenteId, r.ParametroActividadId)).ToDictionary(g => g.Key, g => g.First());
        var idsTiro = tiroPorElem.Values.Distinct().ToList();
        var registrosTiro = db.RegistrosParametroTiro.AsNoTracking().Where(r => idsTiro.Contains(r.TiroId)).ToList()
            .GroupBy(r => (r.TiroId, r.ParametroActividadId)).ToDictionary(g => g.Key, g => g.First());

        decimal? Pct(bool completado, int? estado) =>
            !completado ? null : (estado is int e && pctEstado.TryGetValue(e, out var p) ? p : 0);

        var r = new List<TorrePie>(filas.Count);
        foreach (var f in filas)
        {
            var celdas = new List<decimal?>();
            int? el = f.ElementoTrazadoId;
            int? hoja = el is int e1 && hojaPorElem.TryGetValue(e1, out var h) ? h : null;
            var patas = el is int e2 && patasPorElem.TryGetValue(e2, out var pl) ? pl : new List<int>();
            int? tiro = el is int e3 && tiroPorElem.TryGetValue(e3, out var t) ? t : null;
            foreach (var a in actividades)
            {
                if (!parametros.TryGetValue(a.Id, out var ps)) continue;
                switch (a.Ambito)
                {
                    case AmbitoActividad.Torre:
                        if (hoja is int c)
                            foreach (var p in ps)
                                celdas.Add(registros.TryGetValue((c, p), out var rg) ? Pct(rg.Completado, rg.EstadoParametroId) : null);
                        break;
                    case AmbitoActividad.Pata:
                        foreach (var pa in patas.Where(pa => Aplica(pa, a.Id)))
                            foreach (var p in ps)
                                celdas.Add(registros.TryGetValue((pa, p), out var rg) ? Pct(rg.Completado, rg.EstadoParametroId) : null);
                        break;
                    default:
                        if (tiro is int ti)
                            foreach (var p in ps)
                                celdas.Add(registrosTiro.TryGetValue((ti, p), out var rt) ? Pct(rt.Completado, rt.EstadoParametroId) : null);
                        break;
                }
            }
            var aplican = celdas.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            r.Add(new TorrePie(f.Id, f.Nombre, el, aplican.Count == 0 ? null : aplican.Average(), aplican.Count, aplican.Count(x => x < 100)));
        }
        return r;
    }

    public static string Formato(decimal? v) => v is null ? "—" : $"{v.Value:0.#}%";
}
