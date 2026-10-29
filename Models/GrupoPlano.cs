namespace SigWeb.Models;

// Disciplina/clasificación del listado maestro de planos (Estructural, Civil,
// Eléctrico…). Cada PlanoMaestro pertenece a uno (GrupoPlanoId); la vista
// "Todos" los muestra juntos. Como Grupos de ensayo, cada uno tiene un ícono.
public class GrupoPlano
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
