using ClosedXML.Excel;

namespace SigWeb.Data.Planos;

// Una fila cruda leída del listado maestro de planos. El encabezado de
// este Excel es simple (una sola fila: CODIGO, TITULO DEL DOCUMENTO,
// FECHA, REVISION VIGENTE, ESTADO, NOTAS, REVISIÓN ANTERIOR...), así que
// a diferencia de la tabla de torres no hace falta un mapeo manual: se
// reconoce cada columna por su nombre, con algunas variantes toleradas.
public class FilaListadoPlanos
{
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public DateTime? Fecha { get; set; }
    public string RevisionVigente { get; set; } = "";
    public string Estado { get; set; } = "";
    public string? Notas { get; set; }
    public string? RevisionAnterior { get; set; }
    // Columnas del Excel que no son del listado estándar: encabezado → valor (se vuelven campos "Excel" de la tabla).
    public Dictionary<string, string> Extras { get; set; } = new();
}

public static class LectorExcelListadoPlanos
{
    public static List<FilaListadoPlanos> Leer(Stream excelStream, out List<string> advertencias)
    {
        advertencias = new List<string>();
        var resultado = new List<FilaListadoPlanos>();

        using var wb = new XLWorkbook(excelStream);
        var ws = wb.Worksheet(1);
        var rango = ws.RangeUsed();
        if (rango is null) return resultado;

        int primeraFila = rango.FirstRow().RowNumber();
        int ultimaFila = rango.LastRow().RowNumber();
        int primeraColumna = rango.FirstColumn().ColumnNumber();
        int ultimaColumna = rango.LastColumn().ColumnNumber();

        // Busca la fila de encabezado (la primera que tenga una celda
        // parecida a "CODIGO"), por si el Excel trae filas de título arriba.
        int filaEncabezado = -1;
        var indice = new Dictionary<string, int>(); // nombre normalizado -> columna

        for (int f = primeraFila; f <= Math.Min(ultimaFila, primeraFila + 9); f++)
        {
            var candidato = new Dictionary<string, int>();
            for (int c = primeraColumna; c <= ultimaColumna; c++)
            {
                var texto = Normalizar(ws.Cell(f, c).GetString());
                if (string.IsNullOrWhiteSpace(texto)) continue;
                candidato[texto] = c;
            }
            if (candidato.Keys.Any(k => k.Contains("CODIGO")))
            {
                filaEncabezado = f;
                indice = candidato;
                break;
            }
        }

        if (filaEncabezado == -1)
        {
            advertencias.Add("No se encontró una columna 'CODIGO' en las primeras filas del archivo.");
            return resultado;
        }

        int colCodigo = BuscarColumna(indice, "CODIGO");
        int colTitulo = BuscarColumna(indice, "TITULO DEL DOCUMENTO", "TITULO", "DESCRIPCION");
        int colFecha = BuscarColumna(indice, "FECHA");
        int colRevisionVigente = BuscarColumna(indice, "REVISION VIGENTE", "REVISION");
        int colEstado = BuscarColumna(indice, "ESTADO");
        int colNotas = BuscarColumna(indice, "NOTAS", "OBSERVACIONES");
        int colRevisionAnterior = BuscarColumna(indice, "REVISION ANTERIOR");

        if (colCodigo == -1)
        {
            advertencias.Add("No se encontró la columna 'CODIGO'.");
            return resultado;
        }
        if (colTitulo == -1) advertencias.Add("No se encontró una columna de título; quedará vacío.");

        // Columnas adicionales (no reconocidas): se conservan con su encabezado original.
        var usadas = new HashSet<int> { colCodigo, colTitulo, colFecha, colRevisionVigente, colEstado, colNotas, colRevisionAnterior };
        var extras = new List<(int Col, string Encabezado)>();
        for (int c = primeraColumna; c <= ultimaColumna; c++)
        {
            if (usadas.Contains(c)) continue;
            var enc = ws.Cell(filaEncabezado, c).GetString().Trim();
            if (enc.Length == 0 || enc.Contains('[') || enc.Contains(']')) continue;
            if (extras.Any(x => string.Equals(x.Encabezado, enc, StringComparison.OrdinalIgnoreCase))) continue;
            extras.Add((c, enc));
        }

        for (int f = filaEncabezado + 1; f <= ultimaFila; f++)
        {
            var codigo = ws.Cell(f, colCodigo).GetString().Trim();
            if (string.IsNullOrWhiteSpace(codigo)) continue;

            DateTime? fecha = null;
            if (colFecha != -1)
            {
                var celda = ws.Cell(f, colFecha);
                if (celda.TryGetValue<DateTime>(out var fechaValor)) fecha = fechaValor;
            }

            resultado.Add(new FilaListadoPlanos
            {
                Codigo = codigo,
                Titulo = colTitulo != -1 ? ws.Cell(f, colTitulo).GetString().Trim() : "",
                Fecha = fecha,
                RevisionVigente = colRevisionVigente != -1 ? ws.Cell(f, colRevisionVigente).GetString().Trim() : "",
                Estado = colEstado != -1 ? ws.Cell(f, colEstado).GetString().Trim() : "",
                Notas = colNotas != -1 ? ws.Cell(f, colNotas).GetString().Trim() : null,
                RevisionAnterior = colRevisionAnterior != -1 ? ws.Cell(f, colRevisionAnterior).GetString().Trim() : null,
                Extras = extras.ToDictionary(x => x.Encabezado, x => ws.Cell(f, x.Col).GetFormattedString().Trim(), StringComparer.OrdinalIgnoreCase)
            });
        }

        return resultado;
    }

    private static int BuscarColumna(Dictionary<string, int> indice, params string[] nombresPosibles)
    {
        foreach (var nombre in nombresPosibles)
        {
            var normalizado = Normalizar(nombre);
            if (indice.TryGetValue(normalizado, out var col)) return col;
            // Coincidencia parcial (ej. "REVISION VIGENTE" dentro de un encabezado más largo).
            var parcial = indice.FirstOrDefault(kv => kv.Key.Contains(normalizado));
            if (!string.IsNullOrEmpty(parcial.Key)) return parcial.Value;
        }
        return -1;
    }

    private static string Normalizar(string texto) => texto
        .Trim().ToUpperInvariant()
        .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U").Replace("Ñ", "N");
}
