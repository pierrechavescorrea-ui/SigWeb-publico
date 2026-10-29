using ClosedXML.Excel;
using SigWeb.Models;

namespace SigWeb.Data.Riesgos;

// Una fila cruda de la matriz de riesgos en Excel.
public class FilaMatrizRiesgos
{
    public string Item { get; set; } = "";
    public string EstadoActual { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Area { get; set; } = "";
    public string Causa { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public TipoRiesgo Tipo { get; set; } = TipoRiesgo.Riesgo;
    public string Consecuencia { get; set; } = "";
    public string ProbabilidadTexto { get; set; } = "";
    public string ImpactoTexto { get; set; } = "";
    public ProbabilidadRiesgo? Probabilidad { get; set; }
    public ImpactoRiesgo? Impacto { get; set; }
    public string Disparadores { get; set; } = "";
    public string Estrategia { get; set; } = "";
    public string PlanRespuesta { get; set; } = "";
    public string ResponsablePlan { get; set; } = "";
    public string EstadoPlan { get; set; } = "";
    public string ComoSeHaceSeguimiento { get; set; } = "";
    public int FilaExcel { get; set; }
}

// Lee la matriz de riesgos de la empresa (JE Jaimes / SGI).
//
// Estructura que se reconoce, leída del archivo entregado:
//   · Fila 1: títulos de bloque ("ANÁLISIS CUALITATIVO", "ACCIONES").
//   · Fila 2: encabezados de columna (item, Estado Actual, Categoría, Área,
//     Causa, Riesgo / Oportunidad, Consecuencia, Probabilidad, Impacto,
//     Matriz, …, Disparadores, Estrategia de Respuesta, Plan de Respuesta,
//     Responsable Plan de Respuesta, Estado Plan de Respuesta, Seguimiento).
//   · Debajo del encabezado, cada riesgo ocupa un bloque de varias filas:
//     la primera trae los datos y las siguientes son el dibujo de la matriz
//     5×5, que aquí no se lee porque el nivel se calcula de probabilidad ×
//     impacto.
//
// NO se asume que el bloque mida siempre 7 filas: en el libro real hay hojas
// (p. ej. "CONSTRUCCIÓN 29-04-2022 V1") donde se insertaron o borraron filas
// y los bloques quedaron de 1, 6, 8 o 9 filas. Por eso se recorre fila por
// fila y se toma como riesgo TODA fila que tenga descripción; así no se
// pierde ninguno. El encabezado se ubica por nombre de columna, de modo que
// también tolera que la tabla empiece en otra columna o en otra fila.
public static class LectorExcelMatrizRiesgos
{
    public static List<FilaMatrizRiesgos> Leer(Stream excelStream, out List<string> advertencias)
    {
        using var wb = new XLWorkbook(excelStream);
        return LeerHoja(wb.Worksheet(1), out advertencias);
    }

    // Misma lectura sobre una hoja ya abierta (la usa el lector del libro
    // completo, que recorre una hoja por revisión).
    public static List<FilaMatrizRiesgos> LeerHoja(IXLWorksheet ws, out List<string> advertencias)
    {
        advertencias = new List<string>();
        var filas = new List<FilaMatrizRiesgos>();

        var rango = ws.RangeUsed();
        if (rango is null) { advertencias.Add("La hoja está vacía."); return filas; }

        int ultimaFila = rango.LastRow().RowNumber();
        int ultimaCol = rango.LastColumn().ColumnNumber();

        // ---- Fila de encabezados: la que tenga "Riesgo" y "Probabilidad" ----
        int filaEnc = 0;
        var col = new Dictionary<string, int>();
        for (int r = rango.FirstRow().RowNumber(); r <= Math.Min(ultimaFila, 10); r++)
        {
            var mapa = new Dictionary<string, int>();
            for (int c = 1; c <= ultimaCol; c++)
            {
                var t = Norm(ws.Cell(r, c).GetFormattedString());
                if (t.Length > 0 && !mapa.ContainsKey(t)) mapa[t] = c;
            }
            if (mapa.Keys.Any(k => k.StartsWith("riesgo")) && mapa.ContainsKey("probabilidad"))
            {
                filaEnc = r; col = mapa; break;
            }
        }
        if (filaEnc == 0)
        {
            advertencias.Add("No se encontró la fila de encabezados (se esperaba una fila con \"Riesgo / Oportunidad\" y \"Probabilidad\").");
            return filas;
        }

        int C(params string[] nombres)
        {
            foreach (var n in nombres)
            {
                var k = Norm(n);
                if (col.TryGetValue(k, out var c)) return c;
                var parcial = col.FirstOrDefault(x => x.Key.StartsWith(k));
                if (parcial.Value > 0) return parcial.Value;
            }
            return 0;
        }

        int cItem = C("item"), cEstado = C("estado actual"), cCategoria = C("categoria"), cArea = C("area");
        int cCausa = C("causa"), cRiesgo = C("riesgo / oportunidad", "riesgo"), cConsec = C("consecuencia");
        int cProb = C("probabilidad"), cImp = C("impacto");
        int cDisp = C("disparadores"), cEstrat = C("estrategia de respuesta", "estrategia");
        int cPlan = C("plan de respuesta"), cResp = C("responsable plan de respuesta", "responsable");
        int cEstPlan = C("estado plan de respuesta", "estado plan"), cSeg = C("seguimiento");

        if (cRiesgo == 0) { advertencias.Add("No se encontró la columna \"Riesgo / Oportunidad\"."); return filas; }

        string V(int r, int c) => c == 0 ? "" : (ws.Cell(r, c).GetFormattedString() ?? "").Trim();

        var hayAlguno = false;
        for (int r = filaEnc + 1; r <= ultimaFila; r++)
        {
            var desc = V(r, cRiesgo);
            if (desc.Length == 0) continue;   // fila del dibujo 5×5 o bloque de plantilla vacío
            hayAlguno = true;

            var tipo = TipoRiesgo.Riesgo;
            // El texto viene como "RIESGO: ..." u "OPORTUNIDAD: ..."; se separa el prefijo.
            var sinPrefijo = desc;
            var dosPuntos = desc.IndexOf(':');
            if (dosPuntos > 0 && dosPuntos <= 12)
            {
                var pref = Norm(desc[..dosPuntos]);
                if (pref == "riesgo" || pref == "oportunidad")
                {
                    tipo = pref == "oportunidad" ? TipoRiesgo.Oportunidad : TipoRiesgo.Riesgo;
                    sinPrefijo = desc[(dosPuntos + 1)..].Trim();
                }
            }

            var f = new FilaMatrizRiesgos
            {
                FilaExcel = r,
                Item = V(r, cItem),
                EstadoActual = V(r, cEstado),
                Categoria = V(r, cCategoria),
                Area = V(r, cArea),
                Causa = V(r, cCausa),
                Descripcion = sinPrefijo,
                Tipo = tipo,
                Consecuencia = V(r, cConsec),
                ProbabilidadTexto = V(r, cProb),
                ImpactoTexto = V(r, cImp),
                Disparadores = V(r, cDisp),
                Estrategia = V(r, cEstrat),
                PlanRespuesta = V(r, cPlan),
                ResponsablePlan = V(r, cResp),
                EstadoPlan = V(r, cEstPlan),
                ComoSeHaceSeguimiento = V(r, cSeg)
            };
            f.Probabilidad = EscalaRiesgo.LeerProbabilidad(f.ProbabilidadTexto);
            f.Impacto = EscalaRiesgo.LeerImpacto(f.ImpactoTexto);

            if (f.Probabilidad is null)
                advertencias.Add($"Fila {r}: probabilidad \"{f.ProbabilidadTexto}\" no coincide con la escala (Muy baja, Baja, Moderada, Alta, Muy alta); queda en Moderada.");
            if (f.Impacto is null)
                advertencias.Add($"Fila {r}: impacto \"{f.ImpactoTexto}\" no coincide con la escala (Inferior, Menor, Importante, Mayor, Superior); queda en Importante.");

            filas.Add(f);
        }

        if (!hayAlguno) advertencias.Add("No se encontró ningún riesgo con descripción.");

        // El número de item viene repetido en algunas hojas del libro real
        // (son riesgos distintos con el mismo número). Se avisa; quien importa
        // decide, y el importador les da un código único sin renumerar el resto.
        foreach (var g in filas.Where(f => f.Item.Length > 0).GroupBy(f => f.Item).Where(g => g.Count() > 1))
            advertencias.Add($"El item \"{g.Key}\" está repetido en {g.Count()} riesgos (filas {string.Join(", ", g.Select(x => x.FilaExcel))}).");

        return filas;
    }

    private static string Norm(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "";
        var s = t.Trim().ToLowerInvariant().Replace('\n', ' ');
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s.Normalize(System.Text.NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '/') sb.Append(ch);
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}
