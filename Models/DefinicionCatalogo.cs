namespace SigWeb.Models;

// Un "catálogo" disponible en el módulo Catálogos (lo que antes era la
// lista fija TiposCatalogo en el código). Los 6 catálogos originales
// (Material, Proveedor/Fabricante, Unidad de medida, Criterio de
// aceptación, Documento de referencia, Localización) se siembran una
// sola vez como EsPredeterminado = true; el usuario puede crear los
// suyos desde la pantalla, indicando a qué entidad o propiedad del
// sistema piensa asociarlo y qué campos extra va a necesitar cada ítem
// (ver CampoCatalogoDefinicion).
public class DefinicionCatalogo
{
    public int Id { get; set; }

    // Nombre visible y también la clave que usa ItemCatalogo.Tipo para
    // enlazar sus ítems con este catálogo (igual que antes, cuando era
    // un string fijo).
    public string Nombre { get; set; } = string.Empty;

    // Texto libre: a qué entidad, propiedad o elemento del sistema
    // piensa aplicarse este catálogo (ej. "Material — unidad de medida",
    // "Trazado — tipo de cimentación"). Solo informativo por ahora; no
    // conecta automáticamente con ningún módulo.
    public string EntidadAsociada { get; set; } = string.Empty;

    // Los 6 catálogos de fábrica: no se pueden eliminar desde la UI
    // (sí renombrar su descripción/entidad asociada).
    public bool EsPredeterminado { get; set; } = false;

    public bool Activo { get; set; } = true;
    public int Orden { get; set; } = 0;

    // Ícono del catálogo en la vista por cuadros (galería de íconos). Null = ícono genérico.
    public int? IconoCatalogoId { get; set; }

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
