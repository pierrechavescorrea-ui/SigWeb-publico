namespace SigWeb.Models;

// Qué tipo de dato es un campo extra de un catálogo (CampoCatalogoDefinicion.Tipo).
public enum TipoCampoCatalogo
{
    Texto = 0,
    Numero = 1,
    Fecha = 2,
    SiNo = 3
}

// Un campo extra que los ítems de un DefinicionCatalogo van a tener,
// además de Nombre/Descripción/Estado (que todo ítem de catálogo ya
// trae de por sí). Ej.: el catálogo "Fabricantes" podría tener los
// campos "NIT" (Texto) y "Año de certificación" (Numero). El valor de
// cada campo, por cada ítem, se guarda en ValorCampoCatalogoItem.
public class CampoCatalogoDefinicion
{
    public int Id { get; set; }
    public int DefinicionCatalogoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoCampoCatalogo Tipo { get; set; } = TipoCampoCatalogo.Texto;
    public int Orden { get; set; } = 0;
}
