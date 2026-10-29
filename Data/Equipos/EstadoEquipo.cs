using SigWeb.Models;

namespace SigWeb.Data.Equipos;

// Reglas de "calibración vigente" y estado de vencimiento (mismo texto que la
// matriz de Excel: "Vencido hace N días" / "Faltan N días").
public static class EstadoEquipo
{
    // La vigente es la calibración más reciente (a igual fecha, la registrada después).
    public static IntervencionEquipo? Vigente(IEnumerable<IntervencionEquipo> intervenciones)
        => intervenciones.Where(i => i.Tipo == TipoIntervencionEquipo.Calibracion)
                         .OrderByDescending(i => i.Fecha).ThenByDescending(i => i.Id).FirstOrDefault();

    public static (string Texto, string Clase) Estado(IntervencionEquipo? vigente, DateTime? hoy = null)
    {
        if (vigente?.ProximaFecha is not DateTime prox) return ("Sin fechas", "text-bg-secondary");
        var dias = (int)Math.Round((prox.Date - (hoy ?? DateTime.Today).Date).TotalDays);
        if (dias < 0) return ($"Vencido hace {-dias} días", "text-bg-danger");
        if (dias <= 30) return ($"Faltan {dias} días", "text-bg-warning");
        return ($"Faltan {dias} días", "text-bg-success");
    }

    public static DateTime? ProponerProxima(DateTime fecha, int? meses)
        => meses is int m && m > 0 ? fecha.AddMonths(m) : null;

    // Clave estable desde una etiqueta: "No. de serie" -> "NoDeSerie"
    public static string ClaveDesdeEtiqueta(string etiqueta)
    {
        var norm = etiqueta.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        var mayus = true;
        foreach (var c in norm)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c) && c < 128) { sb.Append(mayus ? char.ToUpperInvariant(c) : c); mayus = false; }
            else mayus = true;
        }
        var clave = sb.ToString();
        return clave.Length == 0 ? "Campo" : (char.IsDigit(clave[0]) ? "C" + clave : clave);
    }
}
