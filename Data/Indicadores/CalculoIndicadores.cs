using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Comunicados;
using SigWeb.Data.Pie;
using SigWeb.Models;

namespace SigWeb.Data.Indicadores;

// Cómo se calcula un indicador según su fuente (Indicador.Configuracion, en JSON).
public class ConfigIndicador
{
    // Comunicados
    public string Sentido { get; set; } = LogicaComunicados.Entrada;   // Entrada | Salida | "" (todos)
    public string? Remitente { get; set; }                             // contiene (vacío = todos)
    public string Calculo { get; set; } = "cobertura";                 // cobertura | oportunidad | tiempo
    public int? PlazoDias { get; set; }                                // null = el de Ajustes de Comunicados

    // Matriz PIE: actual (estado de hoy, todas las celdas) | registro (celdas con fecha de registro en el
    // periodo) | facturado (celdas con fecha de facturado en el periodo). Cada mes puede cambiarlo.
    public string BasePie { get; set; } = "actual";

    // Planes de acción (vacío = todos)
    public List<string> TiposHallazgo { get; set; } = new();
    public List<string> Origenes { get; set; } = new();
    public List<string> Procesos { get; set; } = new();

    public static ConfigIndicador Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<ConfigIndicador>(json) ?? new(); } catch { return new(); }
    }

    public string Escribir() => JsonSerializer.Serialize(this);
}

public sealed record ResultadoIndicador(decimal? Valor, decimal? Numerador, decimal? Denominador, string Detalle, bool NoAplica = false);

// Datos que necesitan los cálculos, leídos una sola vez por carga de la página (nunca desde el render).
public sealed class ContextoIndicadores
{
    public List<Comunicado> Comunicados { get; init; } = new();
    public Dictionary<int, LogicaComunicados.Calculo> CalculoComunicados { get; init; } = new();
    public LogicaComunicados.Ajustes Ajustes { get; init; } = new(8, "INGETEC", "JEJ");
    public List<PlanAccion> Planes { get; init; } = new();
    public Dictionary<int, EstadoAccionPlan> EstadoPlan { get; init; } = new();
    public CumplimientoPie.Resultado Pie { get; init; } = new(null, 0, 0);
    public List<CumplimientoPie.Celda> PieCeldas { get; init; } = new();
    public DateTime Hoy { get; init; } = DateTime.Today;

    // Mismos valores por defecto que Comunicados (los del Excel).
    public static ContextoIndicadores Cargar(AppDbContext db)
    {
        var cfg = db.Configuracion.AsNoTracking().FirstOrDefault(c => c.Id == 1);
        var aj = new LogicaComunicados.Ajustes(cfg?.ComunicadosPlazoDias ?? 8,
            string.IsNullOrWhiteSpace(cfg?.ComunicadosRespondeSalida) ? "INGETEC" : cfg!.ComunicadosRespondeSalida!,
            string.IsNullOrWhiteSpace(cfg?.ComunicadosRespondeEntrada) ? "JEJ" : cfg!.ComunicadosRespondeEntrada!);
        var hoy = DateTime.Today;
        var com = db.Comunicados.AsNoTracking().ToList();
        var planes = db.PlanesAccion.AsNoTracking().ToList();
        var celdasPie = CumplimientoPie.Celdas(db);
        var acciones = db.AccionesPlan.AsNoTracking().ToList().GroupBy(a => a.PlanAccionId).ToDictionary(g => g.Key, g => g.ToList());
        return new ContextoIndicadores
        {
            Comunicados = com,
            CalculoComunicados = LogicaComunicados.Calcular(com, aj, hoy),
            Ajustes = aj,
            Planes = planes,
            EstadoPlan = planes.ToDictionary(p => p.Id, p => EscalaAccion.EstadoPlan(acciones.TryGetValue(p.Id, out var l) ? l : new List<AccionPlan>())),
            PieCeldas = celdasPie,
            Pie = CumplimientoPie.Resumir(celdasPie),
            Hoy = hoy
        };
    }
}

public static class CalculoIndicadores
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    public static int Meses(FrecuenciaIndicador f) => (int)f;

    // Mes en que se reporta: por hitos y mensual = todos; los demás, al cierre del periodo
    // (bimestral: feb, abr…; trimestral: mar, jun, sep, dic; semestral: jun, dic; anual: dic).
    public static bool EsCierre(Indicador ind, int mes) => Meses(ind.Frecuencia) <= 1 || mes % Meses(ind.Frecuencia) == 0;

    // ¿Se mide en ese mes? Mes de cierre y, si tiene fecha final, que el mes empiece antes de ella.
    public static bool Mide(Indicador ind, int anio, int mes) =>
        EsCierre(ind, mes) && (ind.FechaFin is not DateTime fin || new DateTime(anio, mes, 1) <= fin.Date);

    // Fechas que cubre el periodo que cierra en (anio, mes).
    public static (DateTime Desde, DateTime Hasta) Ventana(Indicador ind, int anio, int mes)
    {
        var hasta = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes));
        if (ind.FechaFin is DateTime fin && fin.Date < hasta) hasta = fin.Date;   // se mide hasta la fecha final
        var n = Math.Max(1, Meses(ind.Frecuencia));
        var desde = new DateTime(anio, mes, 1).AddMonths(-(n - 1));
        if (ind.Acumulado) desde = ind.FechaInicio?.Date ?? DateTime.MinValue;
        return (desde, hasta);
    }

    public static string NombrePeriodo(Indicador ind, int anio, int mes)
    {
        var (d, h) = Ventana(ind, anio, mes);
        if (ind.Acumulado) return ind.FechaInicio is null ? $"Acumulado a {h:dd/MM/yyyy}" : $"Acumulado {d:dd/MM/yyyy} – {h:dd/MM/yyyy}";
        return Meses(ind.Frecuencia) <= 1 ? Mes(anio, mes) : $"{Mes(d.Year, d.Month)} a {Mes(anio, mes)}";
    }

    public static string Mes(int anio, int mes) =>
        new DateTime(anio, mes, 1).ToString("MMM-yy", Es).Replace(".", "");

    public static bool Calculado(FuenteIndicador f) => f is FuenteIndicador.CumplimientoPie or FuenteIndicador.Comunicados or FuenteIndicador.PlanesAccion;

    public static string NombreFuente(FuenteIndicador f) => f switch
    {
        FuenteIndicador.CumplimientoPie => "Matriz PIE",
        FuenteIndicador.Comunicados => "Comunicados",
        FuenteIndicador.PlanesAccion => "Planes de acción",
        FuenteIndicador.CurvaS => "Curva S (se digita)",
        _ => "Manual"
    };

    public static string NombreFrecuencia(FrecuenciaIndicador f) => f switch
    {
        FrecuenciaIndicador.PorHitos => "Por hitos",
        FrecuenciaIndicador.Bimestral => "Bimestral",
        FrecuenciaIndicador.Trimestral => "Trimestral",
        FrecuenciaIndicador.Semestral => "Semestral",
        FrecuenciaIndicador.Anual => "Anual",
        _ => "Mensual"
    };

    // null = no se calcula (manual, Curva S, periodo futuro).
    public static readonly (string Clave, string Nombre)[] BasesPie =
    {
        ("actual", "Estado actual"), ("registro", "Fecha de registro"), ("facturado", "Fecha de facturado")
    };

    public static string NombreBasePie(string? b) => BasesPie.FirstOrDefault(x => x.Clave == b).Nombre ?? "Estado actual";

    // baseCalculo: la elegida para ese mes (MedicionIndicador.BaseCalculo); null = la del indicador.
    public static ResultadoIndicador? Calcular(Indicador ind, int anio, int mes, ContextoIndicadores ctx, string? baseCalculo = null)
    {
        if (!Calculado(ind.Fuente)) return null;
        if (new DateTime(anio, mes, 1) > ctx.Hoy) return null;
        if (!Mide(ind, anio, mes)) return null;
        var (desde, hasta) = Ventana(ind, anio, mes);
        var cfg = ConfigIndicador.Leer(ind.Configuracion);
        return ind.Fuente switch
        {
            FuenteIndicador.CumplimientoPie => Pie(baseCalculo ?? cfg.BasePie, desde, hasta, ctx),
            FuenteIndicador.Comunicados => Comunicados(cfg, desde, hasta, ctx),
            FuenteIndicador.PlanesAccion => Planes(cfg, desde, hasta, ctx),
            _ => null
        };
    }

    private static ResultadoIndicador Pie(string? basePie, DateTime desde, DateTime hasta, ContextoIndicadores ctx)
    {
        if (basePie is "registro" or "facturado")
        {
            var porFecha = basePie == "facturado";
            var celdas = ctx.PieCeldas.Where(c => (porFecha ? c.Facturado : c.Fecha) is DateTime f && f.Date >= desde && f.Date <= hasta).ToList();
            var r = CumplimientoPie.Resumir(celdas);
            var que = porFecha ? "fecha de facturado" : "fecha de registro";
            if (r.Aplican == 0) return new(null, 0, 0, $"Matriz PIE: sin registros con {que} {desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}.", true);
            return new(Math.Round(r.Pct!.Value, 2), r.Aplican, r.Total,
                $"Matriz PIE por {que} {desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}: {r.Aplican} registros; promedio del % de estado.");
        }
        return new(ctx.Pie.Pct is decimal p ? Math.Round(p, 2) : null, ctx.Pie.Aplican, ctx.Pie.Total,
            $"Matriz PIE (estado actual): {ctx.Pie.Aplican} de {ctx.Pie.Total} registros aplican; promedio del % de estado.", ctx.Pie.Pct is null);
    }

    private static ResultadoIndicador Comunicados(ConfigIndicador cfg, DateTime desde, DateTime hasta, ContextoIndicadores ctx)
    {
        var plazo = cfg.PlazoDias ?? ctx.Ajustes.PlazoDias;
        var lista = ctx.Comunicados.Where(c => c.RequiereRespuesta == true && c.Fecha is DateTime f && f.Date >= desde && f.Date <= hasta
                && (string.IsNullOrEmpty(cfg.Sentido) || c.Sentido == cfg.Sentido)
                && (string.IsNullOrWhiteSpace(cfg.Remitente) || (c.Remitente ?? "").Contains(cfg.Remitente.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var sentido = string.IsNullOrEmpty(cfg.Sentido) ? "todos" : cfg.Sentido.ToLowerInvariant();
        var filtro = $"comunicados de {sentido}{(string.IsNullOrWhiteSpace(cfg.Remitente) ? "" : $" de \"{cfg.Remitente.Trim()}\"")} que requieren respuesta, fechados {desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}";
        if (lista.Count == 0) return new(null, 0, 0, $"Sin {filtro}.", true);

        var cerrados = lista.Where(c => ctx.CalculoComunicados.TryGetValue(c.Id, out var k) && k.Estado == LogicaComunicados.Cerrado).ToList();
        int? Dias(Comunicado c) => ctx.CalculoComunicados.TryGetValue(c.Id, out var k) ? k.Dias : null;
        switch (cfg.Calculo)
        {
            case "tiempo":
                var dias = cerrados.Select(Dias).Where(d => d is not null).Select(d => (decimal)d!.Value).ToList();
                if (dias.Count == 0) return new(null, 0, lista.Count, $"Ninguno respondido de {lista.Count} {filtro}.", true);
                return new(Math.Round(dias.Average(), 2), dias.Sum(), dias.Count,
                    $"Promedio de días hasta la respuesta de {dias.Count} respondidos ({lista.Count - cerrados.Count} pendientes) — {filtro}.");
            case "oportunidad":
                var aTiempo = cerrados.Count(c => Dias(c) is int d && d >= 0 && d <= plazo);
                return new(Math.Round(aTiempo * 100m / lista.Count, 2), aTiempo, lista.Count,
                    $"{aTiempo} respondidos en ≤ {plazo} días / {lista.Count} {filtro} × 100.");
            default:
                return new(Math.Round(cerrados.Count * 100m / lista.Count, 2), cerrados.Count, lista.Count,
                    $"{cerrados.Count} respondidos / {lista.Count} {filtro} × 100.");
        }
    }

    private static ResultadoIndicador Planes(ConfigIndicador cfg, DateTime desde, DateTime hasta, ContextoIndicadores ctx)
    {
        static bool En(List<string> sel, string valor) => sel.Count == 0 || sel.Contains(valor.Trim(), StringComparer.OrdinalIgnoreCase);
        static bool AlgunoEn(List<string> sel, string texto) => sel.Count == 0
            || texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(x => sel.Contains(x, StringComparer.OrdinalIgnoreCase));
        var lista = ctx.Planes.Where(p => p.Fecha.Date >= desde && p.Fecha.Date <= hasta
                && En(cfg.TiposHallazgo, p.TipoHallazgo) && En(cfg.Origenes, p.OrigenHallazgo) && AlgunoEn(cfg.Procesos, p.Proceso))
            .ToList();
        var tipos = cfg.TiposHallazgo.Count == 0 ? "todos los tipos" : string.Join(", ", cfg.TiposHallazgo);
        var filtro = $"hallazgos ({tipos}{(cfg.Origenes.Count == 0 ? "" : "; origen: " + string.Join(", ", cfg.Origenes))}{(cfg.Procesos.Count == 0 ? "" : "; proceso: " + string.Join(", ", cfg.Procesos))}) con fecha {desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}";
        if (lista.Count == 0) return new(null, 0, 0, $"Sin {filtro}.", true);
        var cerrados = lista.Count(p => ctx.EstadoPlan.TryGetValue(p.Id, out var e) && e == EstadoAccionPlan.Cerrada);
        return new(Math.Round(cerrados * 100m / lista.Count, 2), cerrados, lista.Count,
            $"{cerrados} cerrados (todas sus acciones cerradas) / {lista.Count} {filtro} × 100.");
    }

    // ¿Cumple la meta? null = sin meta o sin valor.
    public static bool? Cumple(Indicador ind, decimal? valor)
    {
        if (valor is not decimal v || ind.Meta is not decimal m) return null;
        return ind.OperadorMeta switch
        {
            ">" => v > m,
            "<=" => v <= m,
            "<" => v < m,
            "=" => v == m,
            _ => v >= m
        };
    }

    public static string Formato(Indicador ind, decimal? v) => v is not decimal x ? "—" : ind.Unidad switch
    {
        "porcentaje" => x.ToString("0.##", Es) + "%",
        "dias" => x.ToString("0.#", Es) + " d",
        _ => x.ToString("0.##", Es)
    };

    public static string NombreUnidad(string u) => u switch { "porcentaje" => "porcentaje", "dias" => "días", _ => "número" };
}

// Primera vez: los indicadores de la matriz del SGI (imagen de Pierr, 17/10/2026), con sus valores de
// ene-26 y feb-26 congelados. Idempotente: solo si la tabla nunca tuvo indicadores.
public static class SembradorIndicadores
{
    public static void Ejecutar(AppDbContext db)
    {
        if (db.Indicadores.IgnoreQueryFilters().Any()) return;
        // Tipos de hallazgo de no conformidad, si existen en Catálogos (si no, quedan todos y se ajusta en el indicador).
        var nc = db.Catalogos.AsNoTracking().Where(c => c.Tipo == SigWeb.Data.Riesgos.SembradorCatalogosRiesgo.TipoHallazgo).Select(c => c.Nombre).ToList()
            .Where(n => n.Contains("conformidad", StringComparison.OrdinalIgnoreCase) || n.Trim().Equals("NC", StringComparison.OrdinalIgnoreCase)).ToList();
        const string resp = "COORDINADOR QAQC";
        var lista = new List<(Indicador I, (int Mes, decimal? Valor)[] Med)>
        {
            (new Indicador { Nombre = "Cumplimiento del plazo contractual", Formula = "(Avance obtenido / avance programado) × 100", Objetivo = "mayor = 90%", Meta = 90,
                Frecuencia = FrecuenciaIndicador.Mensual, Tendencia = "negativa", TipoIndicador = "Eficacia", Responsable = resp, FuenteInformacion = "PDT", Fuente = FuenteIndicador.CurvaS },
                new[] { (1, (decimal?)null), (2, (decimal?)null) }),
            (new Indicador { Nombre = "Programa de gestión de calidad", Formula = "(No. de registros cargados en la plataforma / No. de registros según avance de torres) × 100", Objetivo = "90%", Meta = 90,
                Frecuencia = FrecuenciaIndicador.Mensual, TipoIndicador = "Eficacia", Responsable = resp, FuenteInformacion = "PLAN DE CALIDAD", Fuente = FuenteIndicador.CumplimientoPie },
                new[] { (1, (decimal?)99m), (2, (decimal?)99m) }),
            (new Indicador { Nombre = "Aumentar la satisfacción del cliente", Formula = "Encuesta de satisfacción del cliente", Objetivo = "90%", Meta = 90,
                Frecuencia = FrecuenciaIndicador.PorHitos, FrecuenciaTexto = "Al 50% y al final del proyecto", TipoIndicador = "Eficacia", Responsable = resp, FuenteInformacion = "PLAN DE CALIDAD", Fuente = FuenteIndicador.Manual },
                new[] { (1, (decimal?)null), (2, (decimal?)null) }),
            (new Indicador { Nombre = "Seguimiento a solicitudes del cliente", Formula = "(Respuestas dadas al GEB-INGETEC / Solicitudes requeridas por el GEB-INGETEC) × 100", Objetivo = "100%", Meta = 100,
                Frecuencia = FrecuenciaIndicador.Mensual, TipoIndicador = "cualitativo y/o cuantitativo", Responsable = resp, FuenteInformacion = "RESULTADO DE ENCUESTA", Fuente = FuenteIndicador.Comunicados,
                Configuracion = new ConfigIndicador { Sentido = LogicaComunicados.Entrada, Calculo = "cobertura" }.Escribir() },
                new[] { (1, (decimal?)100m), (2, (decimal?)100m) }),
            (new Indicador { Nombre = "Cumplimiento al Plan de auditorías", Formula = "(Auditoría ejecutada / Auditoría programada) × 100", Objetivo = "100%", Meta = 100,
                Frecuencia = FrecuenciaIndicador.Semestral, TipoIndicador = "Eficacia", Responsable = resp, FuenteInformacion = "PLAN DE AUDITORIA", Fuente = FuenteIndicador.Manual },
                Array.Empty<(int, decimal?)>()),
            (new Indicador { Nombre = "Cierre efectivo de no conformidades", Formula = "(# de NC cerradas / Total de auditorías levantadas) × 100", Objetivo = "100%", Meta = 100,
                Frecuencia = FrecuenciaIndicador.Mensual, TipoIndicador = "Eficacia", Responsable = resp, FuenteInformacion = "MATRIZ NO CONFORMIDADES", Fuente = FuenteIndicador.PlanesAccion,
                Configuracion = new ConfigIndicador { TiposHallazgo = nc }.Escribir() },
                new[] { (1, (decimal?)83m), (2, (decimal?)83m) }),
        };
        var orden = 0;
        foreach (var (ind, _) in lista) { ind.Orden = ++orden; db.Indicadores.Add(ind); }
        db.SaveChanges();
        var ahora = DateTime.Now;
        foreach (var (ind, med) in lista)
            foreach (var (mes, valor) in med)
                db.MedicionesIndicador.Add(new MedicionIndicador
                {
                    IndicadorId = ind.Id, Anio = 2026, Mes = mes, Valor = valor, NoAplica = valor is null,
                    Congelado = true, FechaCongelado = ahora, Detalle = "Valor de la matriz de indicadores anterior (Excel)."
                });
        db.SaveChanges();
    }
}
