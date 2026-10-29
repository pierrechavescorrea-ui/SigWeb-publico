namespace SigWeb.Models;

// Un instrumento/equipo de medición. Solo el nombre y la periodicidad de
// calibración son fijos; todo lo demás (marca, modelo, serie, ubicación...)
// son CampoEquipo configurables, con su valor en ValorCampoEquipo.
public class Equipo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Cada cuántos meses se calibra; con eso se propone la próxima fecha.
    public int? PeriodicidadMeses { get; set; }

    // Carga de Excel que lo creó; nulo si se creó a mano.
    public int? CargaEquiposId { get; set; }

    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}
