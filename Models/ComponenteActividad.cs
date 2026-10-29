namespace SigWeb.Models;

// Qué actividades (Replanteo, Obra Civil, SPT, Montaje...) aplican a un
// elemento (pata, trincho, gavión...) en particular. Una pata que solo
// tiene obra civil no necesita ver la pestaña "Montaje", por ejemplo.
// Si un Componente no tiene NINGUNA fila aquí, se interpreta como "sin
// restricción configurada todavía" y se le siguen mostrando todas las
// actividades activas (así no se ocultan datos que el usuario nunca
// pidió ocultar). El ítem especial "Hoja de vida" (Componente.EsHojaDeVida)
// ignora esta tabla por completo: siempre muestra todas las actividades.
public class ComponenteActividad
{
    public int Id { get; set; }
    public int ComponenteId { get; set; }
    public int ActividadId { get; set; }
}
