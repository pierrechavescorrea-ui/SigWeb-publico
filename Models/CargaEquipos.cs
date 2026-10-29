namespace SigWeb.Models;

// Una importación de la matriz de equipos desde Excel. Los equipos y
// calibraciones creados por ella guardan su Id, para poder eliminar la carga
// completa; lo creado a mano tiene CargaEquiposId nulo.
public class CargaEquipos
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string? Hoja { get; set; }
    public int? FilaEncabezados { get; set; }

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
