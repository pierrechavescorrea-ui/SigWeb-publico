namespace SigWeb.Models;

// Cuando el texto de una torre que trae un Excel de otro módulo (ej.
// Ensayos) no coincide exactamente con ningún Codigo de ElementoTrazado
// (inconsistencias de digitación entre módulos, ej. "102NN" en un
// informe de laboratorio vs. "102NNN" en Trazado), el usuario confirma
// una vez a qué torre corresponde ese texto y queda guardado aquí, para
// que la próxima carga con el mismo texto resuelva sola, sin preguntar
// de nuevo. Un mismo Texto solo puede apuntar a una torre.
public class AliasTorre
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public int ElementoTrazadoId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
