namespace SigWeb.Models;

public class FotoMaterial
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;
}