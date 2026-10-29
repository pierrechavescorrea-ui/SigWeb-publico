namespace SigWeb.Models;

// Un ítem de control dentro de una actividad (ej. "008 - Actas de
// vecindad" dentro de Replanteo). Se marca como completado por cada
// componente (pata, trincho, etc.) a través de RegistroParametro.
public class ParametroActividad
{
    public int Id { get; set; }
    public int ActividadId { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;

    // ---------- Papelera de reciclaje ----------
    // Filtro global (AppDbContext): un parámetro eliminado no aparece en ninguna pantalla;
    // sus registros (avances por componente) se conservan para poder restaurarlo.
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}