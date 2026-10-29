namespace SigWeb.Models;

// A qué nivel del elemento del trazado aplica una actividad: a la torre
// completa (su "Hoja de vida", que siempre ve todas las de ámbito Torre)
// o a cada pata/elemento individual (que ve todas las de ámbito Pata,
// salvo que se restrinjan vía ComponenteActividad).
public enum AmbitoActividad
{
    Torre = 0,
    Pata = 1,
    Tiro = 2   // conjunto de torres sobre el que se tienden conductor, guarda y fibra óptica
}

// Una fase/actividad de construcción. Las de ámbito Torre son las
// históricas (Replanteo, Obra Civil, SPT, Montaje, Tendido, Pruebas
// Conductor, Pruebas Fibra Óptica...); las de ámbito Pata son propias de
// cada pata/elemento (Ensayos de resistencia a la compresión, Ensayos de
// peso unitario o densidades...). Totalmente editable desde el módulo
// Actividades: se pueden agregar, renombrar o desactivar según cambie el
// procedimiento de la empresa.
public class Actividad
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;
    public AmbitoActividad Ambito { get; set; } = AmbitoActividad.Torre;

    // Si el avance de esta actividad se controla (aparece como columna) en
    // la Matriz PIE (Torre: por torre; Pata: por pata; Tiro: por tiro).
    public bool EnMatrizPie { get; set; } = true;

    // Ícono propio (galería de Catálogos, "Usar en…"); null = el del ámbito.
    public int? IconoCatalogoId { get; set; }

    // Enlace real (no por nombre) con el Tipo de Ensayo que esta actividad
    // representa en Trazado. Solo aplica a actividades de ámbito Pata: así
    // cada Tipo de Ensayo creado en el módulo Ensayos tiene su propia
    // actividad sincronizada automáticamente (ver SincronizadorActividadesEnsayo),
    // y en Trazado se sabe con certeza (sin adivinar por texto) cuándo un
    // elemento ya tiene resultados de ese tipo de ensayo. Null = actividad
    // "libre", no asociada a ningún Tipo de Ensayo (ej. de ámbito Torre, o
    // una creada manualmente por el usuario sin vincularla a un ensayo).
    public int? TipoEnsayoId { get; set; }
    public TipoEnsayo? TipoEnsayo { get; set; }

    // ---------- Papelera de reciclaje ----------
    // Al eliminar: Eliminado = true y Activo = false (así todas las pantallas que
    // ya filtran por Activo dejan de mostrarla sin tocar su código).
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}