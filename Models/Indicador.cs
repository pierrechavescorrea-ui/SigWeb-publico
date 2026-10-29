namespace SigWeb.Models;

// De dónde sale el valor de un indicador.
public enum FuenteIndicador
{
    Manual = 0,           // se digita (encuesta, auditorías…), con adjuntos
    CumplimientoPie = 1,  // % total de la Matriz PIE
    Comunicados = 2,      // respuestas a comunicados (cobertura, oportunidad o tiempo promedio)
    PlanesAccion = 3,     // cierre de hallazgos de Planes de acción (tipos elegidos)
    CurvaS = 4            // avance vs programado (Curva S: aún no existe en la app → se digita)
}

// Cada cuánto se mide. El número = meses del periodo (0 = por hitos: cualquier mes, se digita).
public enum FrecuenciaIndicador
{
    PorHitos = 0,
    Mensual = 1,
    Bimestral = 2,
    Trimestral = 3,
    Semestral = 6,
    Anual = 12
}

// Indicador de gestión (Matrices › Indicadores). Las columnas son las de la matriz de indicadores
// del SGI; Fuente + Configuracion dicen cómo se obtiene el valor (ver Data/Indicadores/CalculoIndicadores).
public class Indicador
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;       // fórmula de medición (texto)
    public string Objetivo { get; set; } = string.Empty;      // como se escribe en la matriz ("mayor = 90%")
    public decimal? Meta { get; set; }                        // número con el que se evalúa (90)
    public string OperadorMeta { get; set; } = ">=";          // >=, >, <=, <, =
    public string Unidad { get; set; } = "porcentaje";        // porcentaje | dias | numero
    public FrecuenciaIndicador Frecuencia { get; set; } = FrecuenciaIndicador.Mensual;
    public string? FrecuenciaTexto { get; set; }              // texto libre ("al 50% y al final del proyecto")
    public string Tendencia { get; set; } = "positiva";
    public string TipoIndicador { get; set; } = "Eficacia";
    public string Responsable { get; set; } = string.Empty;
    public string FuenteInformacion { get; set; } = string.Empty;
    public FuenteIndicador Fuente { get; set; } = FuenteIndicador.Manual;
    public string? Configuracion { get; set; }                // JSON (ConfigIndicador)
    public bool Acumulado { get; set; }                       // true = desde FechaInicio hasta el cierre del periodo
    public DateTime? FechaInicio { get; set; }
    public int Orden { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    // Inhabilitado: no sale en las vistas (salvo "Ver inactivos"). FechaFin: se mide hasta esa fecha.
    public bool Inactivo { get; set; }
    public DateTime? FechaFin { get; set; }

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Valor de un indicador en un periodo (Anio + Mes = mes de cierre del periodo).
// Manual: el valor digitado. Calculado: solo existe si se congeló (o tiene observaciones/adjuntos);
// sin congelar, la app muestra el cálculo en vivo.
public class MedicionIndicador
{
    public int Id { get; set; }
    public int IndicadorId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public decimal? Valor { get; set; }
    public bool NoAplica { get; set; }
    public decimal? Numerador { get; set; }
    public decimal? Denominador { get; set; }
    public string? Detalle { get; set; }                      // cómo se obtuvo (cálculo congelado)
    // Matriz PIE: con qué se mide este mes (actual | registro | facturado); null = lo del indicador.
    public string? BaseCalculo { get; set; }
    public string? Observaciones { get; set; }
    public bool Congelado { get; set; }
    public DateTime? FechaCongelado { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Soporte de una medición (encuesta, informe de auditoría…).
public class ArchivoMedicionIndicador
{
    public int Id { get; set; }
    public int MedicionIndicadorId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
