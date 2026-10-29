namespace SigWeb.Models;

// Recuerda a qué destino se mapeó cada columna de Excel la última vez
// que se cargó un archivo para este Tipo de Ensayo (ej. "Diseño de
// mezcla" -> columna "Diseño de mezcla"), para que la próxima carga del
// mismo tipo de ensayo proponga el mismo mapeo automáticamente y el
// usuario no tenga que repetirlo cada vez. Cada tipo de ensayo tiene
// las suyas, independientes de las demás (compresión, Proctor, etc. no
// comparten mapeo).
public class MapeoColumnaEnsayo
{
    public int Id { get; set; }
    public int TipoEnsayoId { get; set; }

    // Encabezado de la columna tal cual venía en el Excel (se compara
    // sin importar mayúsculas/tildes al proponer el mapeo).
    public string EncabezadoTexto { get; set; } = string.Empty;

    // "IGNORAR" | "TORRE" | "ELEMENTO" | "INFORME" | "DISENO_MEZCLA" |
    // "CAMPO:<clave>"
    public string Destino { get; set; } = "IGNORAR";
}
