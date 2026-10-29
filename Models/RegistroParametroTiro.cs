namespace SigWeb.Models;

// Estado de un parámetro de una actividad de ámbito Tiro para un tiro
// (equivalente a RegistroParametro, que es por componente de torre/pata).
public class RegistroParametroTiro
{
    public int Id { get; set; }
    public int TiroId { get; set; }
    public int ParametroActividadId { get; set; }

    // true = el parámetro aplica a este tiro; false = N/A en la matriz.
    public bool Completado { get; set; } = false;

    // Null = aplica pero sin estado (0 %).
    public int? EstadoParametroId { get; set; }
    public DateTime? Fecha { get; set; }
    // Fecha en que se facturó la actividad (agrupa lo facturado; Indicadores puede medir el PIE por ella).
    public DateTime? FechaFacturado { get; set; }
    public string? Observaciones { get; set; }
}
