namespace SigWeb.Models;

// El valor de un CampoEnsayo para un ResultadoEnsayo puntual. Mismo
// patrón Clave/Valor que CampoExtraTorre: como la plantilla de campos
// cambia de un tipo de ensayo a otro, no hace falta una migración nueva
// cada vez que se agrega un campo.
public class ValorCampoEnsayo
{
    public int Id { get; set; }
    public int ResultadoEnsayoId { get; set; }
    public int CampoEnsayoId { get; set; }
    public string Valor { get; set; } = string.Empty;

    // Solo se usa en campos de tipo Fórmula cargados desde Excel: el
    // valor que traía la hoja, para poder compararlo contra el
    // recalculado por el sistema y que el usuario elija cuál usar si no
    // coinciden. Queda vacío en captura manual (ahí no hay "valor de
    // Excel" con qué comparar) y también una vez resuelta la comparación.
    public string? ValorExcelOriginal { get; set; }

    // Adjunto de este valor (compartido entre valores con el mismo archivo).
    public int? AdjuntoEnsayoId { get; set; }
}
