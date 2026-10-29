namespace SigWeb.Models;

public class FotoEquipo
{
    public int Id { get; set; }
    public int EquipoId { get; set; }
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;
}
