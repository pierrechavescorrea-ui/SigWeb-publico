namespace SigWeb.Models;

// Un archivo (plano, foto, PDF...) adjunto directamente a un componente
// (normalmente una pata), sin pasar por un parámetro/checklist. Pensado
// para el "Plano por pata de cimentación" y similares.
public class ArchivoComponente
{
    public int Id { get; set; }
    public int ComponenteId { get; set; }

    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;

    // ---------- Papelera de reciclaje ----------
    // "Eliminar" no borra el archivo físico ni la fila: solo las marca.
    // El borrado físico definitivo ocurre desde la Papelera (o al vencer
    // el plazo de retención).
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
