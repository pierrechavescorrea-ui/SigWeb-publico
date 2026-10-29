namespace SigWeb.Models;

// Campo configurable de la ficha de un equipo (Marca, Modelo, No. de serie...).
// Reutiliza TipoDatoCampoEnsayo (solo Texto, Numero y Fecha aplican aquí).
public class CampoEquipo
{
    public int Id { get; set; }

    // Identificador estable (sin espacios ni tildes); no cambia al renombrar la Etiqueta.
    public string Clave { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
    public TipoDatoCampoEnsayo TipoDato { get; set; } = TipoDatoCampoEnsayo.Texto;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    // Si se puede escribir directo en la Vista Excel del listado.
    public bool EditableEnVistaExcel { get; set; } = true;
}
