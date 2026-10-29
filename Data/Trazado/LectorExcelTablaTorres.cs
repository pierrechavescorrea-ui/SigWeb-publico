using ClosedXML.Excel;

namespace SigWeb.Data.Trazado;

// Una columna detectada en la "tabla de torres" (Excel con muchísimos
// campos: tipo de cimentación, SPT, coordenadas por pata, planos, etc.).
// El encabezado de esa tabla no siempre está en una sola fila: a veces
// un título general ("PATAS") ocupa la primera celda de un grupo de
// columnas, y las letras (A, B, C, D) van en la fila de abajo, cada una
// en su propia columna. Por eso se leen varias filas de encabezado (la
// cantidad la elige el usuario) y se guardan tal cual, sin intentar
// adivinar cómo se combinan: eso lo decide el usuario en la pantalla de
// mapeo, viendo el encabezado completo y un valor de ejemplo.
public class ColumnaTablaTorres
{
    public int Indice { get; set; } // Número de columna de Excel (1 = A, 2 = B, ...)
    public List<string> Encabezados { get; set; } = new(); // Una entrada por fila de encabezado leída
    public string? Ejemplo { get; set; } // Valor de la primera fila de datos, para previsualizar

    public string Direccion => DireccionColumna(Indice);

    // Texto combinado de las líneas de encabezado no vacías, para mostrar
    // y como base de la sugerencia automática de mapeo.
    public string EncabezadoVisible => string.Join(" — ", Encabezados.Where(t => !string.IsNullOrWhiteSpace(t)));

    public static string DireccionColumna(int numero)
    {
        string letras = "";
        while (numero > 0)
        {
            int resto = (numero - 1) % 26;
            letras = (char)('A' + resto) + letras;
            numero = (numero - 1) / 26;
        }
        return letras;
    }
}

public class TablaTorresCruda
{
    public List<ColumnaTablaTorres> Columnas { get; set; } = new();

    // Cada fila de datos, alineada 1 a 1 con Columnas (misma posición).
    public List<string[]> Filas { get; set; } = new();
}

public static class LectorExcelTablaTorres
{
    public static TablaTorresCruda Leer(Stream excelStream, int filasEncabezado)
    {
        var resultado = new TablaTorresCruda();
        if (filasEncabezado < 1) filasEncabezado = 1;

        using var wb = new XLWorkbook(excelStream);
        var ws = wb.Worksheet(1);
        var rango = ws.RangeUsed();
        if (rango is null) return resultado;

        int primeraColumna = rango.FirstColumn().ColumnNumber();
        int ultimaColumna = rango.LastColumn().ColumnNumber();
        int primeraFilaDatos = filasEncabezado + 1;
        int ultimaFila = rango.LastRow().RowNumber();

        for (int col = primeraColumna; col <= ultimaColumna; col++)
        {
            var columna = new ColumnaTablaTorres { Indice = col };
            for (int fila = 1; fila <= filasEncabezado; fila++)
            {
                columna.Encabezados.Add(TextoCelda(ws.Cell(fila, col)));
            }
            if (ultimaFila >= primeraFilaDatos)
            {
                columna.Ejemplo = TextoCelda(ws.Cell(primeraFilaDatos, col));
            }
            resultado.Columnas.Add(columna);
        }

        for (int fila = primeraFilaDatos; fila <= ultimaFila; fila++)
        {
            var valores = new string[ultimaColumna - primeraColumna + 1];
            bool filaVacia = true;
            for (int col = primeraColumna; col <= ultimaColumna; col++)
            {
                var texto = TextoCelda(ws.Cell(fila, col));
                valores[col - primeraColumna] = texto;
                if (!string.IsNullOrWhiteSpace(texto)) filaVacia = false;
            }
            if (!filaVacia) resultado.Filas.Add(valores);
        }

        return resultado;
    }

    private static string TextoCelda(IXLCell celda)
    {
        if (celda.IsEmpty()) return "";

        if (celda.DataType == XLDataType.Number)
        {
            double v = celda.GetDouble();
            return v == Math.Floor(v) && Math.Abs(v) < 1e15
                ? ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        // Los títulos largos a veces traen saltos de línea dentro de la
        // misma celda (ej. una nota de sistema de referencia); se
        // reemplazan por espacios para que quepan en una sola línea al
        // mostrarlos en la pantalla de mapeo.
        return celda.GetString().Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
