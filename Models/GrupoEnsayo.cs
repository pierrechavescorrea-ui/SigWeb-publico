namespace SigWeb.Models;

// Un grupo de ensayos de laboratorio/campo (Concretos, Suelos...). El
// usuario crea los que necesite desde el módulo Ensayos; cada uno se
// subdivide en TipoEnsayo (Resistencia a la compresión, Slump, Proctor,
// PDC...). Si no elige un ícono, se usa uno genérico por defecto.
public class GrupoEnsayo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? IconoCatalogoId { get; set; }
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
