namespace SigWeb.Models;

// El valor que un ítem de catálogo (ItemCatalogo) tiene para uno de los
// campos extra definidos en su catálogo (CampoCatalogoDefinicion). Igual
// que CampoExtraTorre: Clave/Valor libres en vez de una columna fija por
// campo, para no tener que migrar la base de datos cada vez que alguien
// agrega un campo nuevo a un catálogo.
public class ValorCampoCatalogoItem
{
    public int Id { get; set; }
    public int ItemCatalogoId { get; set; }

    // Coincide con CampoCatalogoDefinicion.Nombre del campo al que pertenece.
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}
