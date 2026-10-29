namespace SigWeb.Models;

// Un archivo adjunto (PDF, foto, etc.) de un RegistroParametro. Un mismo
// registro puede tener varios adjuntos (por eso es su propia tabla y no
// un solo campo RutaArchivo como antes).
public class ArchivoRegistroParametro
{
    public int Id { get; set; }
    public int RegistroParametroId { get; set; }

    // Nombre tal como lo subió el usuario (lo que se muestra en pantalla).
    public string NombreOriginal { get; set; } = string.Empty;

    // Ruta física/web donde quedó guardado (nombre en disco = GUID, para
    // evitar choques y problemas de caché del navegador).
    public string RutaArchivo { get; set; } = string.Empty;

    // ---------- Papelera de reciclaje ----------
    // "Eliminar" ya no borra el archivo de una vez: se pide confirmación
    // y solo se marca Eliminado = true. El borrado físico definitivo
    // ocurre desde la Papelera (o al vencer el plazo de retención).
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}