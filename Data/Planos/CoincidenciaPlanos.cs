using System.Text.RegularExpressions;

namespace SigWeb.Data.Planos;

// Empareja el nombre de un archivo PDF cargado con el código de una fila
// del listado maestro de planos. Cubre los dos casos que puede traer el
// listado:
//  - El plano está agrupado: el código del listado es un PREFIJO del
//    nombre del archivo (ej. código "GEB-U113-IN000000-L000-DIS4025",
//    archivo "GEB-U113-IN000000-L000-_DIS4025_H25_R0.pdf").
//  - El plano fue listado de forma independiente: el nombre del archivo
//    coincide (casi) exactamente con el código.
// En ambos casos se normaliza (mayúsculas, espacios/guiones bajos como
// guiones, sin dobles guiones) y se exige un límite de palabra después
// del código (fin de cadena o guion) para no confundir "DIS400" con
// "DIS4001". Cuando varios códigos calzan, se prefiere el más largo
// (más específico).
public static class CoincidenciaPlanos
{
    public static string Normalizar(string texto)
    {
        texto = texto.Trim().ToUpperInvariant();
        texto = Regex.Replace(texto, "[\\s_]+", "-");
        texto = Regex.Replace(texto, "-{2,}", "-");
        return texto.Trim('-');
    }

    // Quita la extensión y sufijos típicos de "revisión" al final del
    // nombre de archivo (ej. "-R0", "-REV0") solo para comparar; el
    // nombre original completo se sigue guardando tal cual.
    public static string NormalizarNombreArchivo(string nombreArchivo)
    {
        var sinExtension = System.IO.Path.GetFileNameWithoutExtension(nombreArchivo);
        return Normalizar(sinExtension);
    }

    public static bool Coincide(string nombreArchivoNormalizado, string codigoNormalizado)
    {
        if (string.IsNullOrWhiteSpace(codigoNormalizado)) return false;
        return nombreArchivoNormalizado == codigoNormalizado
            || nombreArchivoNormalizado.StartsWith(codigoNormalizado + "-", StringComparison.Ordinal);
    }

    // Devuelve el código que mejor calza (el más largo entre los que
    // coinciden), o null si ninguno calza.
    public static T? MejorCoincidencia<T>(string nombreArchivo, IEnumerable<T> candidatos, Func<T, string> obtenerCodigo) where T : class
    {
        var nombreNormalizado = NormalizarNombreArchivo(nombreArchivo);
        return candidatos
            .Select(c => new { Item = c, CodigoNormalizado = Normalizar(obtenerCodigo(c)) })
            .Where(x => Coincide(nombreNormalizado, x.CodigoNormalizado))
            .OrderByDescending(x => x.CodigoNormalizado.Length)
            .Select(x => x.Item)
            .FirstOrDefault();
    }
}
