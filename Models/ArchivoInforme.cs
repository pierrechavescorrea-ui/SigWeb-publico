namespace SigWeb.Models;

// Un PDF (u otro archivo) adjunto a un Informe. Un informe puede traer
// más de uno (documento principal, anexos, certificados...).
public class ArchivoInforme
{
    public int Id { get; set; }
    public int InformeId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;

    // Hash (SHA-256, hex) del contenido del archivo: si el mismo PDF se
    // sube más de una vez (ej. en dos resultados distintos de un mismo
    // lote), solo se guarda una copia física y las demás filas quedan
    // referenciadas a esa misma RutaArchivo — este campo es lo que
    // permite detectar que ya existe.
    public string? Hash { get; set; }
}
