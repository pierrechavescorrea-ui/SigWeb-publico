using System.Text.RegularExpressions;
using SigWeb.Models;

namespace SigWeb.Data.Comunicados;

// Reglas del listado de comunicados, tomadas del Excel de control (SGMTO. COMUNICADOS):
//  · Estado: no requiere respuesta → "N/A"; requiere y sin consecutivo de respuesta → "Pendiente"; con él → "Cerrado".
//  · Días transcurridos: (fecha de respuesta digitada − fecha); sin ella, (hoy − fecha), salvo que ya tenga
//    consecutivo de respuesta: entonces la fecha de ese comunicado. Solo si requiere respuesta.
//  · Plazo: "A tiempo" si los días están entre 0 y el plazo configurado (8 en el Excel); si pasan, "Vencida".
//  · Responsable: Salida → quien responde las salidas; Entrada → quien responde las entradas (configurable).
//  · Respuesta sugerida (columna AUTO del Excel): otro comunicado cuya descripción contiene este consecutivo.
//    Aquí además se ignoran espacios alrededor de los guiones y se prefiere el de sentido contrario,
//    con fecha igual o posterior y el más cercano en el tiempo.
public static class LogicaComunicados
{
    public const string Tabla = "comunicados";
    public const string NA = "N/A", Pendiente = "Pendiente", Cerrado = "Cerrado";
    public const string ATiempo = "A tiempo", Vencida = "Vencida";
    public const string Entrada = "Entrada", Salida = "Salida";

    // ---------- Códigos (los mismos patrones de los scripts del Excel) ----------
    private static readonly Regex BloqueCodigo = new(@"\b[0-9A-Z]+(?:\s*-\s*[0-9A-Z]+){1,6}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EspacioIndebido = new(@"-\s+|\s+-", RegexOptions.Compiled);

    // Clave de comparación de un consecutivo: mayúsculas, sin espacios alrededor de los guiones ni repetidos.
    public static string Clave(string? codigo) =>
        Regex.Replace(Regex.Replace((codigo ?? "").Trim().ToUpperInvariant(), @"\s*-\s*", "-"), @"\s+", " ");

    // ¿El texto tiene códigos con espacios alrededor de los guiones? (detectarCodigosConEspacios)
    public static bool TieneEspaciosIndebidos(string? texto) =>
        !string.IsNullOrEmpty(texto) && BloqueCodigo.Matches(texto).Any(m => EspacioIndebido.IsMatch(m.Value));

    // Quita esos espacios (corregirCodigosConEspacios).
    public static string CorregirCodigos(string texto) =>
        BloqueCodigo.Replace(texto, m => Regex.Replace(m.Value, @"\s*-\s*", "-"));

    public static string? Sentido(string? texto)
    {
        var t = (texto ?? "").Trim().ToUpperInvariant();
        if (t.StartsWith("ENT") || t == "E" || t.Contains("RECIB")) return Entrada;
        if (t.StartsWith("SAL") || t == "S" || t.Contains("ENVIA")) return Salida;
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    public static bool? SiNo(string? texto)
    {
        var t = (texto ?? "").Trim().ToUpperInvariant();
        if (t is "SI" or "SÍ" or "S" or "X" or "1" or "TRUE" or "VERDADERO" or "YES") return true;
        if (t is "NO" or "N" or "0" or "FALSE" or "FALSO") return false;
        return null;
    }

    // ---------- Vista calculada de cada comunicado ----------
    public sealed class Calculo
    {
        public string Estado { get; init; } = "";
        public DateTime? FechaRespuesta { get; init; }   // digitada o la del comunicado de respuesta
        public bool FechaRespuestaDeLista { get; init; }
        public int? Dias { get; init; }
        public string Plazo { get; init; } = "";
        public string Responsable { get; init; } = "";
        public Comunicado? Sugerida { get; init; }       // respuesta sugerida (solo si está pendiente)
        public bool CodigoDuplicado { get; init; }
        public bool EspaciosIndebidos { get; init; }
    }

    public sealed record Ajustes(int PlazoDias, string RespondeSalida, string RespondeEntrada);

    public static Dictionary<int, Calculo> Calcular(IReadOnlyList<Comunicado> lista, Ajustes aj, DateTime hoy)
    {
        var porClave = lista.Where(c => !string.IsNullOrWhiteSpace(c.Consecutivo))
            .GroupBy(c => Clave(c.Consecutivo)).ToDictionary(g => g.Key, g => g.ToList());
        // Descripciones normalizadas una sola vez (para la búsqueda de respuestas).
        var descripciones = lista.Select(c => (C: c, D: Clave(c.Descripcion))).Where(x => x.D.Length > 0).ToList();

        var r = new Dictionary<int, Calculo>(lista.Count);
        foreach (var c in lista)
        {
            var clave = Clave(c.Consecutivo);
            var tieneRta = !string.IsNullOrWhiteSpace(c.ConsecutivoRespuesta);
            var estado = c.RequiereRespuesta switch { false => NA, true => tieneRta ? Cerrado : Pendiente, null => "" };

            // Fecha de respuesta: la digitada o la del comunicado de respuesta (Fecha 2 del Excel).
            DateTime? fr = c.FechaRespuesta;
            bool deLista = false;
            if (fr is null && tieneRta && porClave.TryGetValue(Clave(c.ConsecutivoRespuesta), out var rtas))
            {
                fr = rtas.Select(x => x.Fecha).Where(f => f is not null).OrderBy(f => f).FirstOrDefault();
                deLista = fr is not null;
            }

            int? dias = null;
            if (c.RequiereRespuesta == true && c.Fecha is DateTime f0)
                // Excel: SI(K=0; HOY()−E; K−E). Además, si ya tiene respuesta sin fecha digitada, se usa la
                // fecha del comunicado de respuesta (Fecha 2) en vez de hoy.
                dias = (int)((c.FechaRespuesta ?? (tieneRta ? fr : null) ?? hoy).Date - f0.Date).TotalDays;
            var plazo = dias is int d && c.RequiereRespuesta == true ? (d >= 0 && d <= aj.PlazoDias ? ATiempo : d > aj.PlazoDias ? Vencida : "") : "";

            Comunicado? sug = null;
            if (estado == Pendiente && clave.Length >= 4)
            {
                sug = descripciones
                    .Where(x => x.C.Id != c.Id && x.D.Contains(clave) && Clave(x.C.Consecutivo) != clave
                                && Clave(x.C.Consecutivo) != Clave(c.SugerenciaDescartada))
                    .Select(x => x.C)
                    .OrderByDescending(x => x.Sentido is not null && c.Sentido is not null && x.Sentido != c.Sentido)
                    .ThenByDescending(x => x.Fecha is null || c.Fecha is null || x.Fecha >= c.Fecha)
                    .ThenBy(x => x.Fecha is DateTime fx && c.Fecha is DateTime fc ? Math.Abs((fx - fc).TotalDays) : double.MaxValue)
                    .FirstOrDefault();
            }

            r[c.Id] = new Calculo
            {
                Estado = estado, FechaRespuesta = fr, FechaRespuestaDeLista = deLista, Dias = dias, Plazo = plazo,
                Responsable = c.Sentido == Salida ? aj.RespondeSalida : c.Sentido == Entrada ? aj.RespondeEntrada : "",
                Sugerida = sug,
                CodigoDuplicado = clave.Length > 0 && porClave.TryGetValue(clave, out var mismos) && mismos.Count > 1,
                EspaciosIndebidos = TieneEspaciosIndebidos(c.Consecutivo) || TieneEspaciosIndebidos(c.ConsecutivoRespuesta)
            };
        }
        return r;
    }

    // Áreas responsables de una respuesta ("SST, Técnica, Calidad" → 3).
    public static List<string> Areas(string? texto) =>
        (texto ?? "").Split(new[] { ',', ';', '/', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0).ToList();
}
