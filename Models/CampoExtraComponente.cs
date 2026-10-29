namespace SigWeb.Models;

// Igual que CampoExtraTorre, pero a nivel de un componente (normalmente
// una pata: A, B, C o D). Ej. "Tipo de cimentación" o "Plano de
// cimentación óptima" cargados desde la tabla de torres en Excel.
public class CampoExtraComponente
{
    public int Id { get; set; }
    public int ComponenteId { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;

    // Igual que CampoExtraTorre.Origen: de qué carga vino este valor.
    public string Origen { get; set; } = string.Empty;

    // Igual que CampoExtraTorre: "eliminar" solo marca esto (va a la
    // Papelera, recuperable 30 días). También se usa automáticamente al
    // re-importar una tabla de torres: los campos de una carga anterior
    // que ya no vienen en el Excel nuevo se marcan aquí en vez de quedar
    // mezclados para siempre con los de la carga actual.
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
