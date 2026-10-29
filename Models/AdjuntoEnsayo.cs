namespace SigWeb.Models;

// Archivo adjunto de un campo de ensayo. Se guarda una sola vez por contenido
// (Hash SHA-256): si el mismo archivo se carga para varios elementos de una
// muestra, todos los valores apuntan a este mismo registro.
public class AdjuntoEnsayo
{
    public int Id { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string NombreOriginal { get; set; } = string.Empty;
    public string Ruta { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;
}
