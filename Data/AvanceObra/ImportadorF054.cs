using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Documentos;
using SigWeb.Models;

namespace SigWeb.Data.AvanceObra;

public class ResultadoF054
{
    public DateTime? FechaReporte { get; set; }
    public int TorresLeidas { get; set; }
    public int TorresCasadas { get; set; }
    public int Nuevos { get; set; }
    public int Actualizados { get; set; }
    public int InformesCreados { get; set; }
    public int FechasInvalidas { get; set; }
    public List<string> Actividades { get; set; } = new();          // actividades encontradas en el libro
    public List<string> ActividadesSinColumna { get; set; } = new(); // del módulo que el libro no trae
    public List<string> TorresSinCasar { get; set; } = new();
    public List<string> Aproximadas { get; set; } = new();          // "1N → 1"
    public List<string> TorresSinPatas { get; set; } = new();
    public bool Encabezado { get; set; }                              // se leyó el Reporte Diario
}

// Lee el GEB ENL-DIP-PRO-021-F-054 (hoja "Avance acumulado" y, si está, "Reporte Diario") y lo vuelve
// registros de avance. Por cada torre, actividad y pata (Fecha ini · Avance · Fecha fin):
//   NA → No aplica a la fecha del reporte · inicio → 0 % a la fecha ini · avance → a la fecha fin
//   (sin fecha fin: a la fecha del reporte). Avance 0 sin fechas = no iniciada (no se registra).
// Cada fecha va a su informe diario (lo crea si no existe). Volver a importar actualiza lo mismo
// (torre, pata, actividad, fecha) en vez de duplicar.
public static class ImportadorF054
{
    public const string Marca = "F-054";
    private static readonly Regex RxPata = new(@"^\s*pata\s*([A-Da-d])\b", RegexOptions.IgnoreCase);

    private record Bloque(ActividadObra Act, int Col, bool PorPata);
    private record Plan(int Torre, int? Comp, int Act, DateTime Fecha, decimal Avance, bool NoAplica);

    public static ResultadoF054 Importar(AppDbContext db, Stream xlsx)
    {
        var res = new ResultadoF054();
        using var wb = new XLWorkbook(xlsx);
        var hoja = wb.Worksheets.FirstOrDefault(w => Norm(w.Name) == "avance acumulado")
            ?? throw new InvalidOperationException("El libro no tiene la hoja \"Avance acumulado\".");
        var rd = wb.Worksheets.FirstOrDefault(w => Norm(w.Name) == "reporte diario");

        // Fecha del reporte: la del encabezado del Reporte Diario (DD MM AA); si no, la mayor fecha del libro.
        var fechaRep = rd is null ? null : FechaEncabezado(rd);

        var acts = db.ActividadesObra.AsNoTracking().Where(a => a.Activo).OrderBy(a => a.Orden).ToList();
        var ultimaFila = Math.Min(hoja.LastRowUsed()?.RowNumber() ?? 0, 3000);
        var ultimaCol = hoja.LastColumnUsed()?.ColumnNumber() ?? 0;

        // Columnas: la torre y, por actividad, dónde empieza su bloque (nombre en las filas 1–4 del encabezado).
        int colTorre = 0;
        var bloques = new List<Bloque>();
        for (int f = 1; f <= 5; f++)
            for (int c = 1; c <= ultimaCol; c++)
            {
                var t = Norm(Texto(hoja.Cell(f, c)));
                if (t.Length == 0) continue;
                if (t == "torre" && colTorre == 0) colTorre = c;
                var a = acts.FirstOrDefault(x => Norm(x.Nombre) == t);
                if (a is null || bloques.Any(b => b.Act.Id == a.Id)) continue;
                // Por pata: debajo dice "A ó 1"; por torre: "Fecha ini" en la columna o la siguiente.
                int? pata = null;
                for (int k = 0; k < 3 && pata is null; k++)
                    for (int ff = f + 1; ff <= f + 2; ff++)
                        if (Norm(Texto(hoja.Cell(ff, c + k))).StartsWith("a o 1")) { pata = c + k; break; }
                if (pata is int p) { bloques.Add(new Bloque(a, p, true)); continue; }
                for (int k = 0; k < 2; k++)
                    if (Enumerable.Range(f, 3).Any(ff => Norm(Texto(hoja.Cell(ff, c + k))) == "fecha ini"))
                    { bloques.Add(new Bloque(a, c + k, false)); break; }
            }
        if (colTorre == 0) throw new InvalidOperationException("No se encontró la columna TORRE en \"Avance acumulado\".");
        res.Actividades = bloques.Select(b => b.Act.Nombre).ToList();
        res.ActividadesSinColumna = acts.Where(a => bloques.All(b => b.Act.Id != a.Id)).Select(a => a.Nombre).ToList();

        // Torres del Trazado por nombre/código normalizado.
        var elementos = db.ElementosTrazado.AsNoTracking().Where(e => !e.Eliminado).Select(e => new { e.Id, e.Codigo, e.Nombre }).ToList();
        var porClave = new Dictionary<string, int>();
        var porRaiz = new Dictionary<string, List<int>>();
        foreach (var e in elementos)
            foreach (var n in new[] { e.Nombre, e.Codigo }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            {
                var k = ClaveTorre(n!);
                if (k.Length == 0) continue;
                porClave.TryAdd(k, e.Id);
                var r = Raiz(k);
                if (!porRaiz.TryGetValue(r, out var l)) porRaiz[r] = l = new();
                if (!l.Contains(e.Id)) l.Add(e.Id);
            }
        // Alias de torre (los mismos que usan otros importadores): mandan sobre la coincidencia por raíz.
        var vivos = elementos.Select(e => e.Id).ToHashSet();
        foreach (var a in db.AliasTorres.AsNoTracking().Select(a => new { a.Texto, a.ElementoTrazadoId }).ToList())
            if (vivos.Contains(a.ElementoTrazadoId) && ClaveTorre(a.Texto) is { Length: > 0 } k) porClave.TryAdd(k, a.ElementoTrazadoId);
        var nombreDe = elementos.ToDictionary(e => e.Id, e => string.IsNullOrWhiteSpace(e.Nombre) ? e.Codigo : e.Nombre!);
        var patas = db.Componentes.AsNoTracking().Where(c => !c.Eliminado && !c.EsHojaDeVida).Select(c => new { c.Id, c.ElementoTrazadoId, c.Nombre }).ToList()
            .Select(c => (c.Id, c.ElementoTrazadoId, M: RxPata.Match(c.Nombre))).Where(c => c.M.Success)
            .GroupBy(c => c.ElementoTrazadoId)
            .ToDictionary(g => g.Key, g => g.GroupBy(c => c.M.Groups[1].Value.ToUpperInvariant()).ToDictionary(x => x.Key, x => x.First().Id));

        var planes = new List<Plan>();
        var maxFecha = DateTime.MinValue;
        var filas = new List<(int Torre, Func<int, IXLCell> Celda)>();
        for (int f = 6; f <= ultimaFila; f++)
        {
            var nom = Texto(hoja.Cell(f, colTorre)).Trim();
            if (nom.Length == 0) continue;
            var nn = Norm(nom);
            if (nn.Contains("rendimiento") || nn.StartsWith("total") || nn.Contains("avance")) break;
            res.TorresLeidas++;
            var clave = ClaveTorre(nom);
            int torre;
            if (porClave.TryGetValue(clave, out var id)) torre = id;
            else if (porRaiz.TryGetValue(Raiz(clave), out var ids) && ids.Count == 1) { torre = ids[0]; res.Aproximadas.Add($"{nom} → {nombreDe[torre]}"); }
            else { res.TorresSinCasar.Add(nom); continue; }
            res.TorresCasadas++;
            var fila = f;
            filas.Add((torre, c => hoja.Cell(fila, c)));
        }

        // Fechas válidas: desde 2015 hasta la del reporte (o hasta hoy si no se conoce).
        var tope = (fechaRep ?? DateTime.Today).Date;
        DateTime? Fecha(IXLCell c, ref bool invalida)
        {
            var d = LeerFecha(c);
            if (d is null) { if (Texto(c).Trim().Length > 0) invalida = true; return null; }
            if (d.Value.Year < 2015 || d.Value.Date > tope) { invalida = true; return null; }
            return d.Value.Date;
        }

        var sinFecha = new List<(int Torre, int? Comp, int Act, decimal? Ini, decimal Avance, bool NoAplica, DateTime? FIni)>();
        foreach (var (torre, celda) in filas)
        {
            var pt = patas.TryGetValue(torre, out var pp) ? pp : null;
            foreach (var b in bloques)
            {
                var triples = b.PorPata
                    ? new[] { "A", "B", "C", "D" }.Select((l, k) => (Letra: (string?)l, Col: b.Col + 3 * k))
                    : new[] { (Letra: (string?)null, Col: b.Col) };
                foreach (var (letra, col) in triples)
                {
                    int? comp = null;
                    if (letra is not null)
                    {
                        if (pt is null || !pt.TryGetValue(letra, out var cid))
                        { if (!res.TorresSinPatas.Contains(nombreDe[torre])) res.TorresSinPatas.Add(nombreDe[torre]); continue; }
                        comp = cid;
                    }
                    bool inv = false;
                    var ini = Fecha(celda(col), ref inv);
                    var av = LeerAvance(celda(col + 1), out var na);
                    var fin = Fecha(celda(col + 2), ref inv);
                    if (inv) res.FechasInvalidas++;
                    foreach (var d in new[] { ini, fin }) if (d is DateTime x && x > maxFecha) maxFecha = x;
                    if (na) { sinFecha.Add((torre, comp, b.Act.Id, null, 0, true, null)); continue; }
                    if (av is null && ini is null) continue;
                    var v = av ?? 0;
                    if (v <= 0 && ini is null) continue;                      // no iniciada
                    if (fin is DateTime ff)
                    {
                        if (ini is DateTime i0 && i0 < ff) planes.Add(new Plan(torre, comp, b.Act.Id, i0, 0, false));
                        planes.Add(new Plan(torre, comp, b.Act.Id, ff, v, false));
                    }
                    else sinFecha.Add((torre, comp, b.Act.Id, null, v, false, ini));
                }
            }
        }
        var fechaCorte = fechaRep?.Date ?? (maxFecha > DateTime.MinValue ? maxFecha : DateTime.Today);
        res.FechaReporte = fechaCorte;
        foreach (var x in sinFecha)
        {
            if (x.FIni is DateTime i0 && i0 < fechaCorte) planes.Add(new Plan(x.Torre, x.Comp, x.Act, i0, 0, false));
            planes.Add(new Plan(x.Torre, x.Comp, x.Act, fechaCorte, x.Avance, x.NoAplica));
        }
        // Un solo registro por clave (si el libro repite, gana el último leído).
        planes = planes.GroupBy(p => (p.Torre, p.Comp, p.Act, p.Fecha)).Select(g => g.Last()).ToList();

        // Informes por fecha.
        var fechas = planes.Select(p => p.Fecha).Append(fechaCorte).Distinct().ToList();
        var informes = db.InformesDiarios.Where(i => fechas.Contains(i.Fecha.Date)).ToList()
            .GroupBy(i => i.Fecha.Date).ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).First());
        foreach (var d in fechas.Where(d => !informes.ContainsKey(d)))
        {
            var i = new InformeDiario { Fecha = d, Observaciones = d == fechaCorte ? null : "Reconstruido del avance acumulado del F-054." };
            db.InformesDiarios.Add(i); informes[d] = i; res.InformesCreados++;
        }
        var notas = new List<NotaInformeArea>();
        if (rd is not null && fechaRep is not null) res.Encabezado = LeerEncabezado(rd, informes[fechaCorte], notas);
        db.SaveChanges();
        if (notas.Count > 0)
        {
            // Actividades complementarias del día: reemplazan las del informe.
            var idInf = informes[fechaCorte].Id;
            db.NotasInformeArea.RemoveRange(db.NotasInformeArea.Where(n => n.InformeDiarioId == idInf));
            foreach (var n in notas) { n.InformeDiarioId = idInf; db.NotasInformeArea.Add(n); }
        }

        // Registros: se actualiza el de la misma torre, pata, actividad y fecha; si no, se crea.
        var torres = planes.Select(p => p.Torre).Distinct().ToList();
        var existentes = db.RegistrosAvance.Where(r => torres.Contains(r.ElementoTrazadoId) && !r.Programado).ToList()
            .GroupBy(r => (r.ElementoTrazadoId, r.ComponenteId, r.ActividadObraId, r.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());
        foreach (var p in planes)
        {
            var pct = Math.Round(Math.Clamp(p.Avance, 0, 100), 2);
            if (existentes.TryGetValue((p.Torre, p.Comp, p.Act, p.Fecha), out var r))
            {
                if (r.AvanceAcumulado == pct && r.NoAplica == p.NoAplica) continue;
                r.AvanceAcumulado = pct; r.NoAplica = p.NoAplica; res.Actualizados++;
            }
            else
            {
                db.RegistrosAvance.Add(new RegistroAvance
                {
                    InformeDiarioId = informes[p.Fecha].Id, Fecha = p.Fecha, ElementoTrazadoId = p.Torre, ComponenteId = p.Comp,
                    ActividadObraId = p.Act, AvanceAcumulado = pct, NoAplica = p.NoAplica, Observaciones = Marca
                });
                res.Nuevos++;
            }
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return res;
    }

    // ---------- Reporte Diario: encabezado, actividades complementarias y firmas ----------

    private static DateTime? FechaEncabezado(IXLWorksheet rd)
    {
        foreach (var c in rd.Range(1, 1, 30, 30).CellsUsed())
            if (Norm(Texto(c)).StartsWith("fecha"))
            {
                var nums = new List<int>();
                for (int k = 1; k <= 12 && nums.Count < 3; k++)
                    if (Numero(c.CellRight(k)) is double n) nums.Add((int)n);
                if (nums.Count == 3)
                {
                    var anio = nums[2] < 100 ? 2000 + nums[2] : nums[2];
                    try { return new DateTime(anio, nums[1], nums[0]); } catch { }
                }
                for (int k = 1; k <= 6; k++) if (LeerFecha(c.CellRight(k)) is DateTime d && d.Year >= 2015) return d.Date;
            }
        return null;
    }

    private static bool LeerEncabezado(IXLWorksheet rd, InformeDiario inf, List<NotaInformeArea> notas)
    {
        var celdas = rd.Range(1, 1, Math.Min(rd.LastRowUsed()?.RowNumber() ?? 0, 600), 30).CellsUsed().ToList();
        string? Derecha(IXLCell c, int desde = 1)
        {
            for (int k = desde; k <= 8; k++) { var t = Texto(c.CellRight(k)).Trim(); if (t.Length > 0) return t; }
            return null;
        }
        bool leido = false, firmas = false;
        foreach (var c in celdas)
        {
            var t = Norm(Texto(c));
            if (t.StartsWith("condiciones del")) { inf.ClimaManana = Derecha(c) ?? inf.ClimaManana; leido = true; }
            else if (t == "tarde:" || t == "tarde") inf.ClimaTarde = Derecha(c) ?? inf.ClimaTarde;
            else if (t.StartsWith("hora lluvia") || t.StartsWith("horas lluvia"))
            {
                for (int k = 1; k <= 4; k++) if (Numero(c.CellRight(k)) is double h) { inf.HorasLluvia = (decimal)h; break; }
            }
            else if (!firmas && t.StartsWith("elaborado por"))
            {
                firmas = true;   // solo las primeras (las del control documental del formato van después)
                // Firmas: debajo de ELABORADO / REVISADO / APROBADO POR, en las mismas columnas.
                var fila = c.WorksheetRow();
                foreach (var e in fila.CellsUsed())
                {
                    var et = Norm(Texto(e));
                    var v = Texto(e.CellBelow()).Trim();
                    if (v.Length == 0) continue;
                    if (et.StartsWith("elaborado")) inf.ElaboradoPor = v;
                    else if (et.StartsWith("revisado")) inf.RevisadoPor = v;
                    else if (et.StartsWith("aprobado")) inf.AprobadoPor = v;
                }
                // La fila siguiente a las firmas trae la observación del día (ej. lluvias).
                var obs = rd.Row(fila.RowNumber() + 2).CellsUsed().Select(x => Texto(x).Trim()).FirstOrDefault(x => x.Length > 0);
                if (!string.IsNullOrEmpty(obs) && !Norm(obs).StartsWith("hora")) inf.Observaciones = obs;
            }
        }

        // Actividades complementarias: ítem · área · realizadas · programadas.
        var cab = celdas.FirstOrDefault(c => Norm(Texto(c)) == "actividades complementarias"
            && c.WorksheetRow().CellsUsed().Any(x => Norm(Texto(x)).StartsWith("actividades realizadas")));
        if (cab is not null)
        {
            var areas = ServicioAvanceObra.AreasInforme;
            var filaCab = cab.WorksheetRow();
            int ColDe(string inicio) => filaCab.CellsUsed().FirstOrDefault(x => x.Address.ColumnNumber > cab.Address.ColumnNumber && Norm(Texto(x)).StartsWith(inicio))?.Address.ColumnNumber ?? 0;
            var colReal = ColDe("actividades realizadas");
            var colProg = ColDe("actividades programadas");
            for (int f = cab.Address.RowNumber + 1; f <= cab.Address.RowNumber + 40; f++)
            {
                var area = Texto(rd.Cell(f, cab.Address.ColumnNumber)).Trim();
                var textos = new[] { colReal, colProg }.Select(k => k > 0 ? Texto(rd.Cell(f, k)).Trim() : "").ToList();
                if (rd.Row(f).CellsUsed().Any(x => Norm(Texto(x)).StartsWith("elaborado por"))) break;   // firmas: fin de la tabla
                if (area.Length == 0 || textos.All(x => x.Length == 0)) continue;
                var nombre = areas.FirstOrDefault(a => Norm(a) == Norm(area)) ?? area;
                var orden = Array.IndexOf(areas, nombre);
                notas.Add(new NotaInformeArea
                {
                    Area = nombre, Realizadas = textos.ElementAtOrDefault(0), Programadas = textos.ElementAtOrDefault(1),
                    Orden = orden >= 0 ? orden : areas.Length + notas.Count
                });
            }
            if (notas.Count > 0) leido = true;
        }
        return leido;
    }

    // ---------- Celdas ----------

    private static XLCellValue Valor(IXLCell c) => c.HasFormula ? c.CachedValue : c.Value;

    private static string Texto(IXLCell c)
    {
        try
        {
            var v = Valor(c);
            if (v.IsBlank || v.IsError) return "";
            if (v.IsNumber) return v.GetNumber().ToString(CultureInfo.InvariantCulture);
            if (v.IsDateTime) return v.GetDateTime().ToString("dd/MM/yyyy");
            return v.ToString(CultureInfo.InvariantCulture);
        }
        catch { return ""; }
    }

    private static double? Numero(IXLCell c)
    {
        try { var v = Valor(c); return v.IsNumber ? v.GetNumber() : double.TryParse(Texto(c), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null; }
        catch { return null; }
    }

    private static DateTime? LeerFecha(IXLCell c)
    {
        try
        {
            var v = Valor(c);
            if (v.IsDateTime) return v.GetDateTime();
            if (v.IsNumber) { var n = v.GetNumber(); return n > 20000 && n < 80000 ? DateTime.FromOADate(n) : null; }
            if (v.IsText) return FechaTexto.Parsear(v.GetText());
        }
        catch { }
        return null;
    }

    // Avance en % (0–100). En el libro va como fracción (0–1), "NA" o texto con %.
    private static decimal? LeerAvance(IXLCell c, out bool noAplica)
    {
        noAplica = false;
        var t = Texto(c).Trim();
        if (t.Length == 0) return null;
        var n = Norm(t).Replace("/", "").Replace(".", "");
        if (n is "na" or "no aplica") { noAplica = true; return null; }
        var porcentaje = t.EndsWith('%');
        if (!double.TryParse(t.TrimEnd('%').Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return null;
        var pct = porcentaje || x > 1.0001 ? x : x * 100;
        return (decimal)Math.Clamp(pct, 0, 100);
    }

    // ---------- Nombres ----------

    internal static string Norm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    // "T-15A", "SN-57AN", "Torre 15 A", "154 (Nueva)" → "15A", "57AN", "15A", "154".
    internal static string ClaveTorre(string s)
    {
        var t = Regex.Replace(Norm(s).ToUpperInvariant(), @"\(.*?\)", "");
        t = Regex.Replace(t, @"[\s\-_.#]", "");
        var m = Regex.Match(t, @"\d");
        if (!m.Success) return t;
        t = t[m.Index..];
        return t.TrimStart('0') is { Length: > 0 } z && char.IsDigit(z[0]) ? z : t;
    }

    // Sin las N finales (en el F-054 "1N", "6NN" pueden ser "1", "6" en el Trazado): solo se usa si es única.
    private static string Raiz(string clave) => clave.TrimEnd('N');
}
