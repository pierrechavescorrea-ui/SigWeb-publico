using System.Text.RegularExpressions;
using ClosedXML.Excel;
using SigWeb.Models;

namespace SigWeb.Data.Riesgos;

// Una revisión encontrada en el libro: su hoja de matriz y, si aparece en la
// PORTADA, los datos de su control de cambios.
public class RevisionLibro
{
    public string Hoja { get; set; } = "";
    public string Codigo { get; set; } = "";          // "V5"
    public int? Numero { get; set; }                  // 5
    public string Etapa { get; set; } = "";           // CONSTRUCCIÓN / PRECONSTRUCCIÓN
    public DateTime? FechaHoja { get; set; }          // la del nombre de la hoja
    public DateTime? FechaPortada { get; set; }       // la de la tabla de control de cambios
    public string ElaboradoPor { get; set; } = "";
    public string RevisadoPor { get; set; } = "";
    public string AprobadoPor { get; set; } = "";
    public string EstadoDocumento { get; set; } = "";
    public List<FilaMatrizRiesgos> Filas { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
    public bool Importar { get; set; } = true;

    // Fecha que se usa al importar: manda la PORTADA (es el registro formal
    // de control de cambios); si no está, la del nombre de la hoja.
    public DateTime Fecha => FechaPortada ?? FechaHoja ?? DateTime.Today;
    public bool FechasDiscrepan => FechaPortada is not null && FechaHoja is not null && FechaPortada.Value.Date != FechaHoja.Value.Date;
}

// Listas de validación de la hoja "Datos" del libro.
public class CatalogosLibro
{
    public List<string> EstadoActual { get; set; } = new();
    public List<string> Categoria { get; set; } = new();
    public List<string> Estrategia { get; set; } = new();
    public List<string> EstadoPlan { get; set; } = new();
    public List<string> Tipo { get; set; } = new();          // Alcance / Tiempo / Costo / Calidad
    public bool Hay => EstadoActual.Count + Categoria.Count + Estrategia.Count + EstadoPlan.Count > 0;
}

public class LibroMatrizRiesgos
{
    public List<RevisionLibro> Revisiones { get; set; } = new();
    public CatalogosLibro Catalogos { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}

// Lee el libro completo de la matriz de riesgos de la empresa:
//   · PORTADA  → tabla de control de cambios (versión, fecha, elaborado /
//     revisado / aprobado por, estado documental).
//   · Datos    → listas de validación (Estado Actual, Categoría, Estrategia
//     de Respuesta, Tipo, Estado del plan).
//   · Una hoja por revisión, con el nombre "<ETAPA> <dd-MM-yyyy> V<n>".
// Cada hoja de matriz se lee con LectorExcelMatrizRiesgos, que ubica el
// encabezado por nombre de columna y deduce el paso del bloque, así que
// tolera que las hojas estén desplazadas entre sí (en V5 la tabla arranca
// en la columna B y el encabezado está en la fila 3).
public static class LectorLibroMatrizRiesgos
{
    private static readonly Regex RxVersion = new(@"\bV\s?(\d+)\b", RegexOptions.IgnoreCase);
    private static readonly Regex RxFecha = new(@"(\d{1,2})[-/](\d{1,2})[-/](\d{4})");

    public static LibroMatrizRiesgos Leer(Stream excelStream)
    {
        var libro = new LibroMatrizRiesgos();
        using var wb = new XLWorkbook(excelStream);

        // ---------- Catálogos (hoja "Datos") ----------
        var datos = wb.Worksheets.FirstOrDefault(w => Igual(w.Name, "Datos"));
        if (datos is not null)
        {
            libro.Catalogos.EstadoActual = Columna(datos, "Estado Actual");
            libro.Catalogos.Categoria = Columna(datos, "Categoría", "Categoria");
            libro.Catalogos.Estrategia = Columna(datos, "Estrategia de Respuesta", "Estrategia");
            libro.Catalogos.Tipo = Columna(datos, "Tipo");
            libro.Catalogos.EstadoPlan = Columna(datos, "Estado del plan", "Estado Plan");
        }
        else libro.Advertencias.Add("El libro no tiene hoja \"Datos\": no se pudieron leer las listas de validación.");

        // ---------- Control de cambios (hoja "PORTADA") ----------
        var portada = wb.Worksheets.FirstOrDefault(w => Igual(w.Name, "PORTADA"));
        var control = portada is null ? new Dictionary<int, RevisionLibro>() : LeerPortada(portada);
        if (portada is null) libro.Advertencias.Add("El libro no tiene hoja \"PORTADA\": las revisiones quedan sin firmas ni estado documental.");

        // ---------- Hojas de matriz ----------
        foreach (var ws in wb.Worksheets)
        {
            if (Igual(ws.Name, "PORTADA") || Igual(ws.Name, "Datos") || Igual(ws.Name, "Cuadros") || Igual(ws.Name, "Valoración") || Igual(ws.Name, "Valoracion"))
                continue;

            var rev = new RevisionLibro { Hoja = ws.Name };
            var mv = RxVersion.Match(ws.Name);
            if (mv.Success && int.TryParse(mv.Groups[1].Value, out var n)) { rev.Numero = n; rev.Codigo = "V" + n; }
            else rev.Codigo = ws.Name.Trim();

            var mf = RxFecha.Match(ws.Name);
            if (mf.Success && DateTime.TryParse($"{mf.Groups[3].Value}-{mf.Groups[2].Value}-{mf.Groups[1].Value}", out var f))
                rev.FechaHoja = f;

            rev.Etapa = ws.Name.Split(' ').FirstOrDefault()?.Trim() ?? "";

            if (rev.Numero is int num && control.TryGetValue(num, out var c))
            {
                rev.FechaPortada = c.FechaPortada;
                rev.ElaboradoPor = c.ElaboradoPor;
                rev.RevisadoPor = c.RevisadoPor;
                rev.AprobadoPor = c.AprobadoPor;
                rev.EstadoDocumento = c.EstadoDocumento;
            }

            // Se lee la hoja como matriz; si no tiene la forma esperada, se descarta.
            try
            {
                rev.Filas = LectorExcelMatrizRiesgos.LeerHoja(ws, out var adv);
                rev.Advertencias = adv;
            }
            catch (Exception ex)
            {
                rev.Advertencias.Add("No se pudo leer la hoja: " + ex.Message);
            }

            if (rev.Filas.Count == 0 && rev.Numero is null) continue;   // hoja auxiliar, no es una revisión
            if (rev.FechasDiscrepan)
                rev.Advertencias.Add($"La fecha del nombre de la hoja ({rev.FechaHoja:dd/MM/yyyy}) no coincide con la de la PORTADA ({rev.FechaPortada:dd/MM/yyyy}); se usa la de la PORTADA.");
            libro.Revisiones.Add(rev);
        }

        libro.Revisiones = libro.Revisiones.OrderBy(r => r.Numero ?? int.MaxValue).ThenBy(r => r.Fecha).ToList();
        if (libro.Revisiones.Count == 0) libro.Advertencias.Add("No se encontró ninguna hoja con forma de matriz de riesgos.");
        return libro;
    }

    // Tabla de control de cambios de la PORTADA: se busca la fila de
    // encabezados ("Versión", "Fecha", "Elaborado por"…) y se leen las filas
    // de arriba hacia abajo, que es como está puesta (la más nueva primero).
    private static Dictionary<int, RevisionLibro> LeerPortada(IXLWorksheet ws)
    {
        var mapa = new Dictionary<int, RevisionLibro>();
        var rango = ws.RangeUsed();
        if (rango is null) return mapa;
        int ultF = rango.LastRow().RowNumber(), ultC = rango.LastColumn().ColumnNumber();

        int filaEnc = 0, cVer = 0, cFecha = 0, cElab = 0, cRev = 0, cAprob = 0, cEstado = 0;
        for (int r = 1; r <= ultF; r++)
        {
            for (int c = 1; c <= ultC; c++)
            {
                if (!Norm(ws.Cell(r, c).GetFormattedString()).StartsWith("version")) continue;
                filaEnc = r; cVer = c;
                for (int c2 = c; c2 <= ultC; c2++)
                {
                    var t = Norm(ws.Cell(r, c2).GetFormattedString());
                    if (t.StartsWith("fecha")) cFecha = c2;
                    else if (t.StartsWith("elaborado")) cElab = c2;
                    else if (t.StartsWith("revisado")) cRev = c2;
                    else if (t.StartsWith("aprobado")) cAprob = c2;
                    else if (t.StartsWith("estado")) cEstado = c2;
                }
                break;
            }
            if (filaEnc > 0) break;
        }
        if (filaEnc == 0 || cVer == 0) return mapa;

        // Las filas de datos están ARRIBA del encabezado en esta portada; se
        // recorre toda la hoja y se toma cualquier fila cuya celda de versión
        // sea un número.
        for (int r = 1; r <= ultF; r++)
        {
            if (r == filaEnc) continue;
            var txt = ws.Cell(r, cVer).GetFormattedString().Trim();
            if (!int.TryParse(txt, out var num)) continue;
            var rev = new RevisionLibro { Numero = num, Codigo = "V" + num };
            if (cFecha > 0)
            {
                var cel = ws.Cell(r, cFecha);
                if (cel.TryGetValue<DateTime>(out var d)) rev.FechaPortada = d;
                else if (DateTime.TryParse(cel.GetFormattedString(), out var d2)) rev.FechaPortada = d2;
            }
            if (cElab > 0) rev.ElaboradoPor = ws.Cell(r, cElab).GetFormattedString().Trim();
            if (cRev > 0) rev.RevisadoPor = ws.Cell(r, cRev).GetFormattedString().Trim();
            if (cAprob > 0) rev.AprobadoPor = ws.Cell(r, cAprob).GetFormattedString().Trim();
            if (cEstado > 0) rev.EstadoDocumento = ws.Cell(r, cEstado).GetFormattedString().Trim();
            mapa[num] = rev;
        }
        return mapa;
    }

    // Valores de una lista de la hoja "Datos": la columna cuyo primer valor
    // es el título, de ahí hacia abajo hasta la primera celda vacía.
    private static List<string> Columna(IXLWorksheet ws, params string[] titulos)
    {
        var rango = ws.RangeUsed();
        if (rango is null) return new();
        int ultF = rango.LastRow().RowNumber(), ultC = rango.LastColumn().ColumnNumber();
        foreach (var titulo in titulos)
        {
            var k = Norm(titulo);
            for (int c = 1; c <= ultC; c++)
                for (int r = 1; r <= Math.Min(ultF, 3); r++)
                {
                    if (Norm(ws.Cell(r, c).GetFormattedString()) != k) continue;
                    var lista = new List<string>();
                    for (int r2 = r + 1; r2 <= ultF; r2++)
                    {
                        var v = ws.Cell(r2, c).GetFormattedString().Trim();
                        if (v.Length == 0) break;
                        if (!lista.Contains(v, StringComparer.OrdinalIgnoreCase)) lista.Add(v);
                    }
                    if (lista.Count > 0) return lista;
                }
        }
        return new();
    }

    private static bool Igual(string a, string b) => Norm(a) == Norm(b);

    private static string Norm(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "";
        var s = t.Trim().ToLowerInvariant().Replace('\n', ' ');
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s.Normalize(System.Text.NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch) || ch == ' ') sb.Append(ch);
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}
