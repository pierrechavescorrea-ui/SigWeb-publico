using System.Text;

namespace SigWeb.Data.Dossier;

// Nombres de carpetas y archivos válidos en Windows (donde se descomprime el dossier) y sin repetir
// dentro de una misma carpeta: "Plano" ya usado → "Plano (2)". La pantalla (vista previa) y el
// generador usan la misma clase, así la estructura que se ve es la que llega en el ZIP.
public sealed class NombresZip
{
    public const int MaxCarpeta = 60, MaxArchivo = 100;

    private static readonly HashSet<char> Invalidos = new("<>:\"/\\|?*");
    private static readonly HashSet<string> Reservados = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // carpeta (ruta dentro del ZIP) → nombres ya usados en ella
    private readonly Dictionary<string, HashSet<string>> usados = new(StringComparer.OrdinalIgnoreCase);

    // Limpia un nombre: sin caracteres inválidos ni de control, sin espacios/puntos al final, máx. largo.
    public static string Limpiar(string? texto, int max)
    {
        var sb = new StringBuilder();
        foreach (var ch in (texto ?? "").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' '))
            sb.Append(char.IsControl(ch) || Invalidos.Contains(ch) ? '_' : ch);
        var s = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s{2,}", " ").Trim();
        if (s.Length > max) s = s[..max];
        s = s.TrimEnd(' ', '.');
        if (s.Length == 0) s = "_";
        var raiz = s.Split('.')[0];
        if (Reservados.Contains(raiz)) s = "_" + s;
        return s;
    }

    // Nombre de archivo: limpia y recorta sin perder la extensión.
    public static string LimpiarArchivo(string? nombre)
    {
        var n = (nombre ?? "").Trim();
        var ext = Path.GetExtension(n);
        if (ext.Length > 10 || ext.Any(ch => char.IsWhiteSpace(ch) || Invalidos.Contains(ch))) ext = "";
        var baseN = ext.Length > 0 ? n[..^ext.Length] : n;
        return Limpiar(baseN, MaxArchivo - ext.Length) + ext.ToLowerInvariant();
    }

    // Reserva un nombre único dentro de la carpeta (ruta con "/"). Devuelve la ruta completa.
    public string Unico(string carpeta, string nombre, bool esArchivo)
    {
        if (!usados.TryGetValue(carpeta, out var set)) usados[carpeta] = set = new(StringComparer.OrdinalIgnoreCase);
        var ext = esArchivo ? Path.GetExtension(nombre) : "";
        var baseN = ext.Length > 0 ? nombre[..^ext.Length] : nombre;
        var candidato = nombre;
        for (var i = 2; !set.Add(candidato); i++) candidato = $"{baseN} ({i}){ext}";
        return carpeta.Length == 0 ? candidato : carpeta + "/" + candidato;
    }

    // Une tramos de ruta (ignora vacíos).
    public static string Unir(params string?[] partes) => string.Join("/", partes.Where(p => !string.IsNullOrEmpty(p)));
}
