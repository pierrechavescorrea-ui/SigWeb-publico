using ClosedXML.Excel;

namespace SigWeb.Data.Trazado;

// Una fila cruda del Excel de coordenadas, antes de convertir X/Y a
// latitud/longitud (la conversión depende del sistema que elija el
// usuario, así que aquí solo se lee, no se convierte).
public class PuntoExcelCoordenadas
{
    public string Torre { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

// Lee un Excel con columnas Torre, X, Y (en cualquier orden, detectadas
// por el nombre del encabezado, sin distinguir mayúsculas/tildes).
public static class LectorExcelCoordenadas
{
    public static List<PuntoExcelCoordenadas> Leer(Stream excelStream, out List<string> errores)
    {
        errores = new List<string>();
        var resultado = new List<PuntoExcelCoordenadas>();

        using var wb = new XLWorkbook(excelStream);
        var ws = wb.Worksheet(1);
        var filaEncabezado = ws.Row(1);

        int? colTorre = null, colX = null, colY = null;
        foreach (var celda in filaEncabezado.CellsUsed())
        {
            var texto = QuitarTildes(celda.GetString().Trim().ToLower());
            if (texto == "torre" || texto == "codigo" || texto == "nombre") colTorre ??= celda.Address.ColumnNumber;
            else if (texto == "x" || texto == "este" || texto == "longitud" || texto == "lon") colX ??= celda.Address.ColumnNumber;
            else if (texto == "y" || texto == "norte" || texto == "latitud" || texto == "lat") colY ??= celda.Address.ColumnNumber;
        }

        if (colTorre is null || colX is null || colY is null)
        {
            errores.Add("El Excel debe tener columnas llamadas Torre, X y Y (en la primera fila).");
            return resultado;
        }

        var filas = ws.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();
        int numeroFila = 1;
        foreach (var fila in filas)
        {
            numeroFila++;
            var torre = fila.Cell(colTorre.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(torre)) continue; // fila vacía

            var celdaX = fila.Cell(colX.Value);
            var celdaY = fila.Cell(colY.Value);

            if (!celdaX.TryGetValue<double>(out double x) || !celdaY.TryGetValue<double>(out double y))
            {
                errores.Add($"Fila {numeroFila}: X o Y no es un número válido.");
                continue;
            }

            resultado.Add(new PuntoExcelCoordenadas { Torre = torre, X = x, Y = y });
        }

        return resultado;
    }

    private static string QuitarTildes(string texto)
    {
        return texto
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
    }
}