using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Comunicados;
using SigWeb.Data.Equipos;
using SigWeb.Data.Indicadores;
using SigWeb.Data.Pie;
using SigWeb.Models;

namespace SigWeb.Data.Inicio;

// Datos del tablero de inicio: lo que hay que gestionar hoy en cada módulo. Se lee una sola vez por carga
// (nunca desde el render). Cada apartado trae su total, sus elementos más urgentes y a dónde lleva el clic.
// Tablero dinámico: la bandeja muestra en pestañas solo los apartados con datos (principales primero, luego
// los de reserva); si ningún apartado tiene datos, se ven los principales en verde.
public static class TableroInicio
{
    public const int Ventana = 30;          // días: calibraciones por vencer, riesgos materializados, seguimientos
    public const string Todos = "*";        // responsable "todos" en el ajuste del tablero

    // Tono = clase de color (rojo, ambar, verde, gris).
    // Valor = dato numérico del elemento (Matriz PIE: % de la torre, para la barra); null = nada.
    public sealed record Item(string Titulo, string? Detalle, string Etiqueta, string Tono, string Href, decimal? Valor = null);

    public sealed class Seccion
    {
        public string Clave { get; init; } = "";
        public string Titulo { get; init; } = "";
        public string Icono { get; init; } = "";           // clave de EncabezadoPagina.Trazos (módulo)
        public string Href { get; init; } = "";            // el módulo, ya filtrado
        public int Total { get; init; }
        public string Tono { get; init; } = "gris";
        public List<Item> Items { get; init; } = new();
        public decimal? Pct { get; init; }                 // solo Matriz PIE: cumplimiento total
        public bool Principal { get; init; }
        public bool HayDatos => Total > 0 || Pct is not null;
    }

    // Sub = dato secundario del indicador ("3 vencidas"); null = nada.
    public sealed record Kpi(string Clave, string Titulo, string Valor, string Tono, string Href, string Icono, string? Sub = null);

    public sealed class Datos
    {
        public List<Seccion> Secciones { get; init; } = new();   // principales y luego reserva
        public List<Kpi> Kpis { get; init; } = new();
        public string Responsable { get; init; } = "";             // "" = todos
        public List<string> Responsables { get; init; } = new();
        public Seccion? Pie { get; init; }                         // Matriz PIE (tarjeta propia)
        public CumplimientoPie.Resultado PieTotal { get; init; } = new(null, 0, 0);
        // Mapa de calor de los riesgos activos de la matriz vigente: [probabilidad − 1, impacto − 1].
        public int[,] Mapa { get; init; } = new int[5, 5];
        public int MapaTotal { get; init; }

        // Pestañas de la bandeja: los apartados con pendientes, principales primero y luego los de reserva
        // (un principal vacío cede su lugar). Sin pendientes en ningún módulo: los principales, en verde.
        public List<Seccion> Bandeja()
        {
            var lista = Secciones.Where(s => s.Clave != "pie").ToList();
            var con = lista.Where(s => s.Total > 0).OrderByDescending(s => s.Principal).ToList();
            return con.Count > 0 ? con : lista.Where(s => s.Principal).ToList();
        }
    }

    private static string Corto(string? t, int n = 90)
    {
        var s = (t ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= n ? s : s[..(n - 1)].TrimEnd() + "…";
    }

    private static string Url(string t) => Uri.EscapeDataString(t);

    public static Datos Cargar(AppDbContext db, DateTime? fecha = null)
    {
        var hoy = (fecha ?? DateTime.Today).Date;
        var ctx = ContextoIndicadores.Cargar(db);
        var cfg = db.Configuracion.AsNoTracking().FirstOrDefault(c => c.Id == 1);
        var aj = ctx.Ajustes;
        var respCfg = cfg?.TableroResponsableComunicados;
        var responsable = respCfg == Todos ? "" : string.IsNullOrWhiteSpace(respCfg) ? aj.RespondeEntrada : respCfg.Trim();
        var responsables = new[] { aj.RespondeEntrada, aj.RespondeSalida }
            .Concat(ctx.CalculoComunicados.Values.Select(k => k.Responsable))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var calibracion = Calibraciones(db, hoy);
        var (comVencidos, comPorVencer) = SeccionesComunicados(ctx, responsable);
        var pie = Pie(db, ctx);
        var acciones = db.AccionesPlan.AsNoTracking().ToList();
        var planes = Planes(db, ctx, acciones, hoy);
        var materializados = Materializados(db, hoy);
        var accionesVencidas = AccionesVencidas(ctx, acciones, hoy);
        var indicadores = IndicadoresFueraDeMeta(db, ctx, hoy);
        var sinSeguimiento = RiesgosSinSeguimiento(db, hoy);

        var secciones = new List<Seccion>
        {
            calibracion, comVencidos, pie, planes, materializados,
            accionesVencidas, indicadores, comPorVencer, sinSeguimiento
        };

        static string? Sub(int n, string singular, string plural) => n == 0 ? null : $"{n} {(n == 1 ? singular : plural)}";
        var calVencidas = calibracion.Items.Count(i => i.Tono == "rojo");
        var kpis = new List<Kpi>
        {
            pie.Pct is not null
                ? new Kpi("pie", "Cumplimiento PIE", CumplimientoPie.Formato(pie.Pct), TonoPct(pie.Pct), pie.Href, "matriz",
                    Sub(pie.Total, "torre con pendientes", "torres con pendientes"))
                : new Kpi("acciones", "Acciones vencidas", accionesVencidas.Total.ToString(), accionesVencidas.Total > 0 ? "rojo" : "verde",
                    accionesVencidas.Href, "plan-accion"),
            new("comunicados", "Comunicados vencidos", comVencidos.Total.ToString(), comVencidos.Total > 0 ? "rojo" : "verde", comVencidos.Href,
                "comunicados", Sub(comPorVencer.Total, "por responder", "por responder")),
            new("calibracion", "Calibraciones ≤ 30 días", calibracion.Total.ToString(), calibracion.Tono, calibracion.Href, "equipos",
                Sub(calVencidas, "vencida", "vencidas")),
            new("planes", "Hallazgos y planes abiertos", planes.Total.ToString(), planes.Total > 0 ? "ambar" : "verde", planes.Href, "plan-accion",
                Sub(accionesVencidas.Total, "acción vencida", "acciones vencidas")),
            new("riesgos", "Riesgos materializados", materializados.Total.ToString(), materializados.Total > 0 ? "rojo" : "verde", materializados.Href,
                "riesgos", Sub(sinSeguimiento.Total, "sin seguimiento", "sin seguimiento")),
            new("indicadores", "Indicadores fuera de meta", indicadores.Total.ToString(), indicadores.Total > 0 ? "rojo" : "verde",
                indicadores.Href, "indicadores"),
        };

        var (mapa, mapaTotal) = MapaRiesgos(db);
        return new Datos
        {
            Secciones = secciones, Kpis = kpis, Responsable = responsable, Responsables = responsables,
            Pie = pie.Pct is null ? null : pie, PieTotal = ctx.Pie, Mapa = mapa, MapaTotal = mapaTotal
        };
    }

    // Riesgos activos (no cerrados) de la matriz vigente por probabilidad × impacto.
    private static (int[,] Mapa, int Total) MapaRiesgos(AppDbContext db)
    {
        var mapa = new int[5, 5];
        var vigente = db.VersionesMatrizRiesgos.AsNoTracking().Where(v => v.Estado == EstadoVersionMatriz.Vigente)
            .OrderByDescending(v => v.Fecha).FirstOrDefault();
        if (vigente is null) return (mapa, 0);
        var riesgos = db.Riesgos.AsNoTracking().Where(r => r.VersionMatrizRiesgosId == vigente.Id && r.Tipo == TipoRiesgo.Riesgo).ToList()
            .Where(r => !r.EstadoActual.Contains("cerr", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var r in riesgos) mapa[(int)r.Probabilidad - 1, (int)r.Impacto - 1]++;
        return (mapa, riesgos.Count);
    }

    private static string TonoPct(decimal? p) => p switch { null => "gris", >= 90 => "verde", >= 60 => "ambar", _ => "rojo" };

    // ---------- Equipos: calibración vencida o que vence en los próximos 30 días ----------
    private static Seccion Calibraciones(AppDbContext db, DateTime hoy)
    {
        var equipos = db.Equipos.AsNoTracking().Where(e => !e.Eliminado).ToList();
        var cal = db.IntervencionesEquipo.AsNoTracking().Where(i => i.Tipo == TipoIntervencionEquipo.Calibracion).ToList()
            .GroupBy(i => i.EquipoId).ToDictionary(g => g.Key, g => g.ToList());
        var items = new List<(int Dias, Item Item)>();
        foreach (var e in equipos)
        {
            if (!cal.TryGetValue(e.Id, out var l) || EstadoEquipo.Vigente(l)?.ProximaFecha is not DateTime prox) continue;
            var dias = (int)Math.Round((prox.Date - hoy).TotalDays);
            if (dias > Ventana) continue;
            var cert = EstadoEquipo.Vigente(l)!.NumeroCertificado;
            items.Add((dias, new Item(e.Nombre, $"Vence {prox:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(cert) ? "" : $" · Cert. {cert}"),
                dias < 0 ? $"Vencido {-dias} d" : dias == 0 ? "Hoy" : $"{dias} d", dias < 0 ? "rojo" : "ambar",
                $"equipos/{e.Id}/hoja-de-vida")));
        }
        var orden = items.OrderBy(x => x.Dias).Select(x => x.Item).ToList();
        var vencidos = items.Count(x => x.Dias < 0);
        return new Seccion
        {
            Clave = "calibracion", Titulo = "Calibraciones por vencer", Icono = "equipos", Principal = true,
            Href = "equipos?estado=alerta", Total = orden.Count, Tono = vencidos > 0 ? "rojo" : orden.Count > 0 ? "ambar" : "verde",
            Items = orden
        };
    }

    // ---------- Comunicados del responsable: vencidos (pendientes fuera de plazo) y por vencer ----------
    private static (Seccion Vencidos, Seccion PorVencer) SeccionesComunicados(ContextoIndicadores ctx, string responsable)
    {
        bool DelResponsable(LogicaComunicados.Calculo k) =>
            responsable == "" || string.Equals(k.Responsable, responsable, StringComparison.OrdinalIgnoreCase);
        var pendientes = ctx.Comunicados
            .Select(c => (C: c, K: ctx.CalculoComunicados.TryGetValue(c.Id, out var k) ? k : null))
            .Where(x => x.K is not null && x.K.Estado == LogicaComunicados.Pendiente && DelResponsable(x.K))
            .ToList();
        var plazo = ctx.Ajustes.PlazoDias;
        var filtroResp = responsable == "" ? "" : "&responsable=" + Url(responsable);

        var vencidos = pendientes.Where(x => x.K!.Plazo == LogicaComunicados.Vencida).OrderByDescending(x => x.K!.Dias)
            .Select(x => new Item(x.C.Consecutivo.Trim(), Corto(x.C.Descripcion),
                $"+{(x.K!.Dias ?? 0) - plazo} d", "rojo", "comunicados?buscar=" + Url(x.C.Consecutivo.Trim())))
            .ToList();
        var porVencer = pendientes.Where(x => x.K!.Plazo == LogicaComunicados.ATiempo).OrderByDescending(x => x.K!.Dias)
            .Select(x => new Item(x.C.Consecutivo.Trim(), Corto(x.C.Descripcion),
                $"{plazo - (x.K!.Dias ?? 0)} d", plazo - (x.K!.Dias ?? 0) <= 2 ? "ambar" : "gris", "comunicados?buscar=" + Url(x.C.Consecutivo.Trim())))
            .ToList();
        var quien = responsable == "" ? "" : $" · {responsable}";
        return (
            new Seccion
            {
                Clave = "comunicados", Titulo = "Comunicados vencidos" + quien, Icono = "comunicados", Principal = true,
                Href = $"comunicados?estado={Url(LogicaComunicados.Pendiente)}&plazo={Url(LogicaComunicados.Vencida)}{filtroResp}",
                Total = vencidos.Count, Tono = vencidos.Count > 0 ? "rojo" : "verde", Items = vencidos
            },
            new Seccion
            {
                Clave = "comunicados-plazo", Titulo = "Comunicados por responder" + quien, Icono = "comunicados",
                Href = $"comunicados?estado={Url(LogicaComunicados.Pendiente)}&plazo={Url(LogicaComunicados.ATiempo)}{filtroResp}",
                Total = porVencer.Count, Tono = porVencer.Count > 0 ? "ambar" : "verde", Items = porVencer
            });
    }

    // ---------- Matriz PIE: cumplimiento total y torres con registros pendientes ----------
    private static Seccion Pie(AppDbContext db, ContextoIndicadores ctx)
    {
        var torres = CumplimientoPie.PorTorre(db);
        var items = torres.Where(t => t.Pendientes > 0)
            .OrderByDescending(t => t.Pendientes).ThenBy(t => t.Pct ?? 0)
            .Select(t => new Item(t.Nombre, $"{t.Pendientes} de {t.Aplican} pendientes", t.Pendientes.ToString(),
                TonoPct(t.Pct), $"matriz-pie?torre={t.LocalizacionId}", t.Pct))
            .ToList();
        return new Seccion
        {
            Clave = "pie", Titulo = "Cumplimiento PIE", Icono = "matriz", Principal = true, Href = "matriz-pie",
            Total = items.Count, Pct = ctx.Pie.Pct, Tono = TonoPct(ctx.Pie.Pct), Items = items
        };
    }

    // ---------- Hallazgos (de riesgos) y planes de acción abiertos o en ejecución ----------
    private static Seccion Planes(AppDbContext db, ContextoIndicadores ctx, List<AccionPlan> acciones, DateTime hoy)
    {
        var porPlan = acciones.GroupBy(a => a.PlanAccionId).ToDictionary(g => g.Key, g => g.ToList());
        var items = new List<(int Orden, DateTime Fecha, Item Item)>();
        foreach (var p in ctx.Planes)
        {
            var estado = ctx.EstadoPlan.TryGetValue(p.Id, out var e) ? e : EstadoAccionPlan.Abierta;
            if (estado == EstadoAccionPlan.Cerrada) continue;
            var vencidas = porPlan.TryGetValue(p.Id, out var l) ? l.Count(a => a.Estado != EstadoAccionPlan.Cerrada && a.Fecha.Date < hoy) : 0;
            items.Add((vencidas > 0 ? 0 : estado == EstadoAccionPlan.Abierta ? 1 : 2, p.Fecha, new Item(
                string.IsNullOrWhiteSpace(p.Codigo) ? $"Plan {p.Id}" : p.Codigo,
                Corto(p.DescripcionHallazgo) + (vencidas > 0 ? $" · {vencidas} acción{(vencidas == 1 ? "" : "es")} vencida{(vencidas == 1 ? "" : "s")}" : ""),
                EscalaAccion.Nombre(estado), vencidas > 0 || estado == EstadoAccionPlan.Abierta ? "rojo" : "ambar",
                $"planes-accion/{p.Id}")));
        }

        // Hallazgos registrados en los seguimientos de riesgos, sin cerrar.
        var hallazgos = db.HallazgosRiesgo.AsNoTracking().Where(h => h.Estado != EstadoHallazgo.Cerrado).ToList();
        if (hallazgos.Count > 0)
        {
            var idsSeg = hallazgos.Select(h => h.SeguimientoRiesgoId).Distinct().ToList();
            var segs = db.SeguimientosRiesgo.AsNoTracking().Where(s => idsSeg.Contains(s.Id)).ToDictionary(s => s.Id);
            var idsR = segs.Values.Select(s => s.RiesgoId).Distinct().ToList();
            var riesgos = db.Riesgos.AsNoTracking().Where(r => idsR.Contains(r.Id)).ToDictionary(r => r.Id);
            foreach (var h in hallazgos)
            {
                if (!segs.TryGetValue(h.SeguimientoRiesgoId, out var s) || !riesgos.TryGetValue(s.RiesgoId, out var r)) continue;
                items.Add((h.Estado == EstadoHallazgo.Abierto ? 1 : 2, s.Fecha, new Item($"Hallazgo · {r.Codigo}", Corto(h.Descripcion),
                    h.Estado == EstadoHallazgo.Abierto ? "Abierto" : "En tratamiento", h.Estado == EstadoHallazgo.Abierto ? "rojo" : "ambar",
                    $"riesgos/{r.Id}")));
            }
        }
        var orden = items.OrderBy(x => x.Orden).ThenBy(x => x.Fecha).Select(x => x.Item).ToList();
        return new Seccion
        {
            Clave = "planes", Titulo = "Hallazgos y planes abiertos", Icono = "plan-accion", Principal = true, Href = "planes-accion",
            Total = orden.Count, Tono = orden.Count > 0 ? "ambar" : "verde", Items = orden
        };
    }

    // ---------- Riesgos materializados en los últimos 30 días (seguimientos) ----------
    private static Seccion Materializados(AppDbContext db, DateTime hoy)
    {
        var desde = hoy.AddDays(-Ventana);
        var segs = db.SeguimientosRiesgo.AsNoTracking()
            .Where(s => s.Resultado == ResultadoSeguimiento.Materializado && s.Fecha >= desde && s.Fecha <= hoy.AddDays(1)).ToList();
        var idsR = segs.Select(s => s.RiesgoId).Distinct().ToList();
        var riesgos = db.Riesgos.AsNoTracking().Where(r => idsR.Contains(r.Id)).ToDictionary(r => r.Id);
        var items = segs.Where(s => riesgos.ContainsKey(s.RiesgoId)).OrderByDescending(s => s.Fecha)
            .Select(s =>
            {
                var r = riesgos[s.RiesgoId];
                var nivel = EscalaRiesgo.Nivel(s.Probabilidad ?? r.Probabilidad, s.Impacto ?? r.Impacto);
                return new Item($"{r.Codigo} · {s.Fecha:dd/MM/yyyy}", Corto(string.IsNullOrWhiteSpace(s.Observacion) ? r.Descripcion : s.Observacion),
                    EscalaRiesgo.Nombre(nivel), nivel >= NivelRiesgo.Alto ? "rojo" : "ambar", $"riesgos/{r.Id}");
            }).ToList();
        return new Seccion
        {
            Clave = "materializados", Titulo = "Riesgos materializados (30 días)", Icono = "riesgos", Principal = true, Href = "riesgos",
            Total = items.Count, Tono = items.Count > 0 ? "rojo" : "verde", Items = items
        };
    }

    // ---------- Reserva: acciones de planes con fecha vencida y sin cerrar ----------
    private static Seccion AccionesVencidas(ContextoIndicadores ctx, List<AccionPlan> acciones, DateTime hoy)
    {
        var planes = ctx.Planes.ToDictionary(p => p.Id);
        var items = acciones.Where(a => a.Estado != EstadoAccionPlan.Cerrada && a.Fecha.Date < hoy && planes.ContainsKey(a.PlanAccionId))
            .OrderBy(a => a.Fecha)
            .Select(a => new Item(
                $"{(string.IsNullOrWhiteSpace(planes[a.PlanAccionId].Codigo) ? $"Plan {a.PlanAccionId}" : planes[a.PlanAccionId].Codigo)} · {EscalaAccion.Nombre(a.Tipo)}",
                Corto(a.Descripcion) + (string.IsNullOrWhiteSpace(a.Responsable) ? "" : $" · {a.Responsable}"),
                $"{(hoy - a.Fecha.Date).Days} d", "rojo", $"planes-accion/{a.PlanAccionId}?acciones={a.Id}"))
            .ToList();
        return new Seccion
        {
            Clave = "acciones", Titulo = "Acciones vencidas", Icono = "plan-accion", Href = "planes-accion",
            Total = items.Count, Tono = items.Count > 0 ? "rojo" : "verde", Items = items
        };
    }

    // ---------- Reserva: indicadores cuyo último periodo medido no cumple la meta ----------
    private static Seccion IndicadoresFueraDeMeta(AppDbContext db, ContextoIndicadores ctx, DateTime hoy)
    {
        var indicadores = db.Indicadores.AsNoTracking().Where(i => !i.Inactivo).OrderBy(i => i.Orden).ThenBy(i => i.Id).ToList();
        var mediciones = db.MedicionesIndicador.AsNoTracking().ToList()
            .GroupBy(m => (m.IndicadorId, m.Anio, m.Mes)).ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Id).First());
        var items = new List<Item>();
        foreach (var ind in indicadores)
        {
            // Último mes medido (hasta 12 meses atrás): lo congelado o digitado manda; si no, el cálculo en vivo
            // (misma regla que la celda de Indicadores).
            for (var k = 0; k < 12; k++)
            {
                var f = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-k);
                if (!CalculoIndicadores.Mide(ind, f.Year, f.Month)) continue;
                mediciones.TryGetValue((ind.Id, f.Year, f.Month), out var m);
                decimal? valor;
                if (m?.Congelado == true || !CalculoIndicadores.Calculado(ind.Fuente))
                    valor = m is null || m.NoAplica ? null : m.Valor;
                else
                {
                    var r = CalculoIndicadores.Calcular(ind, f.Year, f.Month, ctx, m?.BaseCalculo);
                    valor = r is null || r.NoAplica ? null : r.Valor;
                }
                if (valor is null) continue;
                if (CalculoIndicadores.Cumple(ind, valor) == false)
                    items.Add(new Item(ind.Nombre, $"{CalculoIndicadores.Mes(f.Year, f.Month)} · meta {ind.Objetivo}",
                        CalculoIndicadores.Formato(ind, valor), "rojo", $"indicadores?indicador={ind.Id}"));
                break;
            }
        }
        return new Seccion
        {
            Clave = "indicadores", Titulo = "Indicadores fuera de meta", Icono = "indicadores", Href = "indicadores",
            Total = items.Count, Tono = items.Count > 0 ? "rojo" : "verde", Items = items
        };
    }

    // ---------- Reserva: riesgos activos de la matriz vigente sin seguimiento en los últimos 30 días ----------
    private static Seccion RiesgosSinSeguimiento(AppDbContext db, DateTime hoy)
    {
        var vigente = db.VersionesMatrizRiesgos.AsNoTracking().Where(v => v.Estado == EstadoVersionMatriz.Vigente)
            .OrderByDescending(v => v.Fecha).FirstOrDefault();
        var items = new List<(NivelRiesgo Nivel, Item Item)>();
        if (vigente is not null)
        {
            var todos = db.Riesgos.AsNoTracking().Select(r => new { r.Id, r.RiesgoAnteriorId }).ToList()
                .ToDictionary(r => r.Id, r => r.RiesgoAnteriorId);
            var ultimo = db.SeguimientosRiesgo.AsNoTracking().GroupBy(s => s.RiesgoId)
                .Select(g => new { g.Key, Fecha = g.Max(s => s.Fecha) }).ToList().ToDictionary(x => x.Key, x => x.Fecha);
            var riesgos = db.Riesgos.AsNoTracking().Where(r => r.VersionMatrizRiesgosId == vigente.Id && r.Tipo == TipoRiesgo.Riesgo).ToList()
                .Where(r => !r.EstadoActual.Contains("cerr", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var r in riesgos)
            {
                // El mismo riesgo en revisiones anteriores cuenta (sus seguimientos quedaron allá).
                DateTime? fecha = null;
                var visto = new HashSet<int>();
                int? id = r.Id;
                while (id is int x && visto.Add(x))
                {
                    if (ultimo.TryGetValue(x, out var fx) && (fecha is null || fx > fecha)) fecha = fx;
                    id = todos.TryGetValue(x, out var ant) ? ant : null;
                }
                if (fecha is DateTime fu && fu.Date >= hoy.AddDays(-Ventana)) continue;
                items.Add((r.Nivel, new Item(r.Codigo, Corto(r.Descripcion),
                    fecha is DateTime u ? $"{(hoy - u.Date).Days} d" : "Sin seguimiento",
                    r.Nivel >= NivelRiesgo.Alto ? "rojo" : r.Nivel == NivelRiesgo.Moderado ? "ambar" : "gris", $"riesgos/{r.Id}")));
            }
        }
        var orden = items.OrderByDescending(x => x.Nivel).Select(x => x.Item).ToList();
        return new Seccion
        {
            Clave = "seguimiento", Titulo = "Riesgos sin seguimiento (30 días)", Icono = "riesgos", Href = "riesgos",
            Total = orden.Count, Tono = orden.Count > 0 ? "ambar" : "verde", Items = orden
        };
    }
}
