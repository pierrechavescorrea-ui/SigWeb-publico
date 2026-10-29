namespace SigWeb.Models;

// Tiro de tendido: conjunto de torres sobre el que se tienden conductor,
// guarda y fibra óptica. Cada torre pertenece a un solo tiro
// (ElementoTrazado.TiroId). Sus actividades (ámbito Tiro) se registran en
// RegistroParametroTiro y se ven en la Matriz PIE.
public class Tiro
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; } = 0;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
