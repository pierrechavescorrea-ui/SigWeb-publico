namespace SigWeb.Models;

// Un ícono disponible para usar en el sistema (grupos y tipos de ensayo,
// y en el futuro cualquier otro módulo que necesite señalizar algo con
// un símbolo). La "galería de íconos" en Catálogos gestiona esta tabla:
// el usuario puede subir sus propios íconos (SVG o PNG), renombrarlos y
// clasificarlos por categoría. Clave es un identificador estable (no se
// muestra) que usan los sembradores para no duplicar íconos ya creados.
public class IconoCatalogo
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;

    // Obra civil, Eléctrico, Torres, Suelos, Concretos, Laboratorio,
    // General... puramente organizativo, no restringe dónde se puede usar.
    public string Categoria { get; set; } = "General";

    public bool Activo { get; set; } = true;
}
