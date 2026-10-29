namespace SigWeb.Models;

// Un tipo de ensayo dentro de un grupo (ej. dentro de "Suelos": Peso
// unitario o densidades, Proctor, PDC). Si IconoCatalogoId es null, se
// muestra con el mismo ícono de su grupo; si el usuario le elige uno
// propio, ese prevalece (ej. Proctor con su propio ícono de martillo,
// dentro del grupo Suelos que usa el ícono de cono y arena).
public class TipoEnsayo
{
    public int Id { get; set; }
    public int GrupoEnsayoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? IconoCatalogoId { get; set; }
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;

    // true (por defecto, para no romper los tipos existentes como
    // Resistencia a la compresión): el formulario de "Agregar resultado"
    // pide Torre + Elemento (pata), ligados a Trazado.
    // false: ensayos que no corresponden a una torre puntual (suelos,
    // pavimentos...) — el formulario pide Localización (texto libre) y
    // Fecha de ejecución en vez de Torre y Diseño de mezcla.
    public bool UsaTorre { get; set; } = true;

    // Visibilidad de las columnas fijas del sistema (no son CampoEnsayo,
    // así que no tienen su propio "Activo" como los campos que el usuario
    // define) — se puede ocultar cada una si no se usa, tanto en Vista
    // Excel como en el diálogo Agregar/Editar. Torre/Localización no se
    // puede ocultar: es la columna de identidad con la que se busca y
    // se cruza con Trazado.
    public bool MostrarElemento { get; set; } = true;
    public bool MostrarInforme { get; set; } = true;
    public bool MostrarFechaEjecucion { get; set; } = true; // solo aplica si !UsaTorre
    public bool MostrarDisenoMezcla { get; set; } = true; // solo aplica si UsaTorre

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
