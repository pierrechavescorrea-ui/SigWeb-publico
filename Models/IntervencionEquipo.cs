namespace SigWeb.Models;

public enum TipoIntervencionEquipo
{
    Calibracion = 0,
    Mantenimiento = 1
}

// Una intervención sobre un equipo (calibración o mantenimiento). Nunca se
// sobrescribe ni se borra al actualizar: cada calibración nueva es otro
// registro y conserva su propio certificado; la vigente es la calibración
// más reciente (ver Data/Equipos/EstadoEquipo.cs).
public class IntervencionEquipo
{
    public int Id { get; set; }
    public int EquipoId { get; set; }
    public TipoIntervencionEquipo Tipo { get; set; } = TipoIntervencionEquipo.Calibracion;
    public DateTime Fecha { get; set; } = DateTime.Today;

    // Solo calibraciones: fecha en que vence esa calibración.
    public DateTime? ProximaFecha { get; set; }

    // Laboratorio que calibró, o quien hizo el mantenimiento.
    public string? Responsable { get; set; }
    public string? NumeroCertificado { get; set; }
    public string? Descripcion { get; set; }

    // Certificado o soporte adjunto (PDF o imagen) y su nombre original.
    public string? RutaCertificado { get; set; }
    public string? NombreCertificado { get; set; }

    // Carga de Excel que la creó; nulo si se registró a mano.
    public int? CargaEquiposId { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera (solo cuando se elimina su carga de Excel) ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
