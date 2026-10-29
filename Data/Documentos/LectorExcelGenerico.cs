using ClosedXML.Excel;

namespace SigWeb.Data.Documentos;

// Lee cualquier libro Excel como texto (lo que se ve en la celda), sin suponer
// dónde están los encabezados: la página deja elegir la fila de encabezados y las columnas.
public static class LectorExcelGenerico
{
    public const int MaxFilas = 20000;
    public const int MaxColumnas = 200;

    public static List<string> Hojas(byte[] libro)
    {
        using var ms = new MemoryStream(libro);
        using var wb = new XLWorkbook(ms);
        return wb.Worksheets.Where(h => h.Visibility == XLWorksheetVisibility.Visible).Select(h => h.Name).ToList();
    }

    // Filas desde la 1 hasta la última usada (máx. MaxFilas), cada una con el mismo número de columnas.
    // Las celdas combinadas toman el valor de su primera celda (útil en encabezados de dos niveles).
    public static List<string[]> LeerHoja(byte[] libro, string hoja)
    {
        using var ms = new MemoryStream(libro);
        using var wb = new XLWorkbook(ms);
        if (!wb.TryGetWorksheet(hoja, out var ws)) return new();
        var usado = ws.RangeUsed();
        if (usado is null) return new();
        int ultimaFila = Math.Min(usado.LastRow().RowNumber(), MaxFilas);
        int ultimaCol = Math.Min(usado.LastColumn().ColumnNumber(), MaxColumnas);

        var combinadas = new Dictionary<(int F, int C), string>();
        foreach (var m in ws.MergedRanges)
        {
            var v = Texto(m.FirstCell());
            foreach (var c in m.Cells()) combinadas[(c.Address.RowNumber, c.Address.ColumnNumber)] = v;
        }

        var filas = new List<string[]>(ultimaFila);
        for (int f = 1; f <= ultimaFila; f++)
        {
            var fila = new string[ultimaCol];
            for (int c = 1; c <= ultimaCol; c++)
                fila[c - 1] = combinadas.TryGetValue((f, c), out var v) ? v : Texto(ws.Cell(f, c));
            filas.Add(fila);
        }
        return filas;
    }

    private static string Texto(IXLCell c)
    {
        // Fechas: siempre día/mes/año. GetFormattedString aplica el formato corto de fecha de Excel
        // (numFmt 14) al estilo EE. UU. (M/d/yyyy): 12/05/2026 salía "5/12/2026".
        try
        {
            if (c.DataType == XLDataType.DateTime)
            {
                var f = c.GetDateTime();
                return f.TimeOfDay == TimeSpan.Zero ? f.ToString("dd/MM/yyyy") : f.ToString("dd/MM/yyyy HH:mm");
            }
        }
        catch { }
        try { return c.GetFormattedString().Trim(); }
        catch { return c.Value.ToString()?.Trim() ?? ""; }
    }

    // A, B, …, Z, AA…
    public static string LetraColumna(int indiceBase0)
    {
        var n = indiceBase0 + 1;
        var s = "";
        while (n > 0) { var r = (n - 1) % 26; s = (char)('A' + r) + s; n = (n - 1) / 26; }
        return s;
    }
}
