namespace SigWeb.Models;

// Archivo adjunto a un material, clasificado (Remisión, Certificado, Registro fotográfico, Otros).
// Reemplaza a los PDF de remisión/certificado y a las fotos (MigracionMateriales los pasa aquí).
public class AdjuntoMaterial
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string Clase { get; set; } = Otros;
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;

    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }

    public const string Remision = "Remisión", Certificado = "Certificado", Fotografico = "Registro fotográfico", Otros = "Otros";
    public static readonly string[] Clases = { Remision, Certificado, Fotografico, Otros };
}
