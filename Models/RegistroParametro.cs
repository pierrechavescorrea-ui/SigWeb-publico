namespace SigWeb.Models;

// El estado de un parámetro de actividad para un componente (pata,
// trincho, etc.) específico: si ya se completó, cuándo, con qué
// observación y con qué soporte adjunto (PDF o foto).
public class RegistroParametro
{
    public int Id { get; set; }
    public int ComponenteId { get; set; }
    public int ParametroActividadId { get; set; }

    // En la Matriz PIE, "Completado" = el parámetro APLICA a esta torre
    // (el check de la hoja de vida). Sin check = "N/A" en la matriz.
    public bool Completado { get; set; } = false;

    // Estado de avance (con su % de peso). Null = aplica pero sin estado (0 %).
    public int? EstadoParametroId { get; set; }
    public DateTime? Fecha { get; set; }
    // Fecha en que se facturó la actividad (agrupa lo facturado; Indicadores puede medir el PIE por ella).
    public DateTime? FechaFacturado { get; set; }
    public string? Observaciones { get; set; }

    // Los archivos adjuntos ya no van aquí (puede haber varios): ver
    // ArchivoRegistroParametro, que apunta a este registro por Id.
}