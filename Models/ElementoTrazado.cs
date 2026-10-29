namespace SigWeb.Models;

// Una torre del trazado, ubicada en el mapa a partir del KMZ. Ya no
// representa patas ni otros sub-elementos: esos ahora son Componente,
// hijos lógicos de la torre que no tienen coordenadas propias.
public class ElementoTrazado
{
    public int Id { get; set; }

    // Código con el que aparece en el KMZ (ej. "T-15"). Es lo que se usa
    // para reconocer la misma torre entre una carga de KMZ y la siguiente.
    public string Codigo { get; set; } = string.Empty;

    public string? Nombre { get; set; }

    public double Latitud { get; set; }
    public double Longitud { get; set; }

    // Ícono/color propios de esta torre en el mapa; si son null, se usa el
    // valor por defecto configurado para "torres" en Trazado.
    public string? IconoMapa { get; set; }
    public string? ColorMapa { get; set; }

    // A qué capa (carga de KMZ/Excel) pertenece la referencia gráfica de
    // esta torre. Null solo puede pasar transitoriamente; el sembrador de
    // capas le asigna una "Cargadas antes de esta función" a cualquier
    // torre que la tenga en null.
    public int? CapaTrazadoId { get; set; }

    // Tiro de tendido al que pertenece la torre (null = sin tiro asignado).
    public int? TiroId { get; set; }

    // ---------- Eliminar solo la referencia gráfica ----------
    // "Eliminar" aquí NO borra los Componentes/checklist/materiales ya
    // cargados en esta torre: solo la saca del mapa (va a la Papelera).
    // Así, si luego se carga otro KMZ/Excel con el mismo código (o uno
    // parecido, vía el flujo de "migrar"), esos datos se recuperan
    // automáticamente sobre la nueva referencia en vez de perderse.
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}