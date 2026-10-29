namespace SigWeb.Models;

// Anexo de la "Ficha técnica" de un equipo (manuales, catálogos, fichas del fabricante…).
public class ArchivoEquipo
{
    public int Id { get; set; }
    public int EquipoId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;

    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
