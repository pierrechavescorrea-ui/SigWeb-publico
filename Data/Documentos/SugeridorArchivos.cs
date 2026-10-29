using System.Text.RegularExpressions;

namespace SigWeb.Data.Documentos;

// Sugiere a qué fila (documento o plano) pertenece un archivo por su nombre: código completo
// contenido en el nombre → todos los números del código → parte de los números → palabras del
// título/nombre de la fila. Lo usan las cargas masivas de archivos de Documentos y Planos.
public static class SugeridorArchivos
{
    public sealed record Indice(int Id, string Etiqueta, string CodigoAlfa, List<string> Numeros, HashSet<string> Palabras);
    public sealed record Candidato(int Id, string Etiqueta, int Puntos);

    public static Indice Indexar(int id, string etiqueta, string? codigo, string? nombre) =>
        new(id, etiqueta, Alfa(codigo ?? ""), Numeros(codigo ?? ""), Palabras(nombre ?? ""));

    public static List<Candidato> Sugerir(string archivo, IEnumerable<Indice> indice, int max = 8)
    {
        var nombre = Path.GetFileNameWithoutExtension(archivo);
        var alfa = Alfa(nombre);
        var nums = Numeros(nombre);
        var palabras = Palabras(nombre);
        var r = new List<Candidato>();
        foreach (var d in indice)
        {
            int p = 0;
            if (d.CodigoAlfa.Length >= 3 && alfa.Contains(d.CodigoAlfa)) p = 1000 + d.CodigoAlfa.Length;
            else if (d.Numeros.Count > 0)
            {
                var iguales = d.Numeros.Where(n => nums.Contains(n)).ToList();
                p = iguales.Count == d.Numeros.Count ? 500 + iguales.Sum(n => n.Length) * 10 : iguales.Sum(n => n.Length) * 15;
            }
            p += d.Palabras.Count(w => palabras.Contains(w)) * 20;
            if (p > 0) r.Add(new Candidato(d.Id, d.Etiqueta, p));
        }
        return r.OrderByDescending(x => x.Puntos).ThenBy(x => x.Etiqueta).Take(max).ToList();
    }

    // Preselección automática solo si la coincidencia es clara (código o todos sus números) y sin empate.
    public static bool EsClara(List<Candidato> c) =>
        c.Count > 0 && c[0].Puntos >= 500 && (c.Count == 1 || c[1].Puntos < c[0].Puntos);

    public static string Coincidencia(int puntos) =>
        puntos >= 1000 ? "código" : puntos >= 500 ? "números del código" : puntos >= 40 ? "parcial" : "débil";

    private static string Alfa(string s) => new string(MigracionDocumentos.Normalizar(s).Where(char.IsLetterOrDigit).ToArray());

    private static List<string> Numeros(string s) =>
        Regex.Matches(s, @"\d+").Select(m => m.Value.TrimStart('0')).Where(x => x.Length > 0).ToList();

    private static HashSet<string> Palabras(string s) =>
        Regex.Split(MigracionDocumentos.Normalizar(s), @"[^a-z0-9]+").Where(w => w.Length >= 4 && !w.All(char.IsDigit)).ToHashSet();
}

// Resultado del panel de importación de Excel: filas de datos y a qué destino va cada columna.
public sealed class ImportacionExcel
{
    public string NombreArchivo { get; init; } = "";
    public string Hoja { get; init; } = "";
    public int FilaEncabezados { get; init; }            // 1 = primera fila del Excel
    public List<string> Encabezados { get; init; } = new();
    public List<string[]> Filas { get; init; } = new();   // solo las filas de datos (bajo los encabezados)
    public Dictionary<int, string> Destinos { get; init; } = new();   // columna → clave del destino ("nueva" = columna nueva)

    public const string Nueva = "nueva";
    public static string Celda(string[] fila, int c) => c < fila.Length ? fila[c] : "";
    public int? ColumnaDe(string destino) => Destinos.Where(kv => kv.Value == destino).Select(kv => (int?)kv.Key).FirstOrDefault();
}

// Opción de destino para una columna del Excel (columna propia o existente), con nombres alternativos para la sugerencia.
public sealed record DestinoColumna(string Clave, string Nombre, params string[] Alias);

// Un archivo elegido en la carga masiva, ya asignado a su fila.
public sealed record ArchivoACargar(Microsoft.AspNetCore.Components.Forms.IBrowserFile Archivo, int FilaId, SigWeb.Models.ClaseArchivoDocumento Clase, string Version);

// Fecha a partir del texto de una celda (lo que muestra Excel): dd/mm/aaaa, aaaa-mm-dd, número de serie de Excel…
public static class FechaTexto
{
    private static readonly string[] Formatos = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yy", "d/M/yy", "dd/MM/yyyy HH:mm", "yyyy-MM-dd HH:mm:ss", "dd-MMM-yy", "dd-MMM-yyyy" };

    public static DateTime? Parsear(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = texto.Trim();
        var es = System.Globalization.CultureInfo.GetCultureInfo("es-CO");
        if (DateTime.TryParseExact(texto, Formatos, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var f)) return f;
        if (DateTime.TryParseExact(texto, Formatos, es, System.Globalization.DateTimeStyles.None, out f)) return f;
        if (double.TryParse(texto, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 20000 && n < 80000) return DateTime.FromOADate(n);
        return DateTime.TryParse(texto, es, System.Globalization.DateTimeStyles.None, out f) ? f : null;
    }
}
