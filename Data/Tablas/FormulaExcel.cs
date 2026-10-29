using System.Globalization;
using System.Text;

namespace SigWeb.Data.Tablas;

// Motor de fórmulas tipo Excel para los campos calculados de cualquier tabla
// (ver CampoTabla). Propio y liviano: sin paquetes externos.
//
// Sintaxis:
//   =SI([Fecha] < HOY(); "Vencido"; "Al día")
//   [Campo]                 referencia a otro campo de la MISMA fila (por su nombre)
//   123.45  "texto"  VERDADERO / FALSO
//   + - * / ^   & (une textos)   = <> < > <= >=   paréntesis
//   Separador de argumentos: ";" o ","  (el decimal siempre es punto)
// Fechas: fecha − fecha = días; fecha ± número = fecha.
// Funciones (español o inglés): ver Funciones() más abajo.
public static class FormulaExcel
{
    public class ErrorFormula : Exception
    {
        public ErrorFormula(string mensaje) : base(mensaje) { }
    }

    // Catálogo de funciones para el asistente: nombre, plantilla, grupo y descripción.
    public record InfoFuncion(string Nombre, string Plantilla, string Grupo, string Descripcion);

    public static readonly IReadOnlyList<InfoFuncion> Funciones = new List<InfoFuncion>
    {
        new("SI", "SI(condición; si_verdadero; si_falso)", "Lógicas", "Devuelve un valor u otro según la condición."),
        new("Y", "Y(cond1; cond2)", "Lógicas", "VERDADERO si todas las condiciones se cumplen."),
        new("O", "O(cond1; cond2)", "Lógicas", "VERDADERO si alguna condición se cumple."),
        new("NO", "NO(condición)", "Lógicas", "Invierte VERDADERO/FALSO."),
        new("SI.ERROR", "SI.ERROR(valor; si_error)", "Lógicas", "Devuelve otro valor si el cálculo da error."),
        new("ESBLANCO", "ESBLANCO(valor)", "Lógicas", "VERDADERO si el valor está vacío."),
        new("HOY", "HOY()", "Fechas", "Fecha de hoy."),
        new("AHORA", "AHORA()", "Fechas", "Fecha y hora actuales."),
        new("FECHA", "FECHA(año; mes; día)", "Fechas", "Arma una fecha."),
        new("DIAS", "DIAS(fecha_final; fecha_inicial)", "Fechas", "Días entre dos fechas."),
        new("FECHA.MES", "FECHA.MES(fecha; meses)", "Fechas", "Suma (o resta) meses a una fecha."),
        new("AÑO", "AÑO(fecha)", "Fechas", "Año de una fecha."),
        new("MES", "MES(fecha)", "Fechas", "Mes de una fecha (1–12)."),
        new("DIA", "DIA(fecha)", "Fechas", "Día del mes."),
        new("DIASEM", "DIASEM(fecha)", "Fechas", "Día de la semana (1 = domingo … 7 = sábado)."),
        new("SUMA", "SUMA(valor1; valor2)", "Matemáticas", "Suma los valores."),
        new("PROMEDIO", "PROMEDIO(valor1; valor2)", "Matemáticas", "Promedio de los valores."),
        new("MIN", "MIN(valor1; valor2)", "Matemáticas", "El menor valor."),
        new("MAX", "MAX(valor1; valor2)", "Matemáticas", "El mayor valor."),
        new("REDONDEAR", "REDONDEAR(número; decimales)", "Matemáticas", "Redondea a n decimales."),
        new("ABS", "ABS(número)", "Matemáticas", "Valor absoluto."),
        new("ENTERO", "ENTERO(número)", "Matemáticas", "Parte entera (hacia abajo)."),
        new("RESIDUO", "RESIDUO(número; divisor)", "Matemáticas", "Resto de la división."),
        new("POTENCIA", "POTENCIA(número; potencia)", "Matemáticas", "Eleva a una potencia."),
        new("RAIZ", "RAIZ(número)", "Matemáticas", "Raíz cuadrada."),
        new("PI", "PI()", "Matemáticas", "3,14159…"),
        new("CONCATENAR", "CONCATENAR(texto1; texto2)", "Texto", "Une textos (también con &)."),
        new("IZQUIERDA", "IZQUIERDA(texto; n)", "Texto", "Primeros n caracteres."),
        new("DERECHA", "DERECHA(texto; n)", "Texto", "Últimos n caracteres."),
        new("EXTRAE", "EXTRAE(texto; inicio; n)", "Texto", "n caracteres desde la posición inicio."),
        new("LARGO", "LARGO(texto)", "Texto", "Cantidad de caracteres."),
        new("MAYUSC", "MAYUSC(texto)", "Texto", "Convierte a mayúsculas."),
        new("MINUSC", "MINUSC(texto)", "Texto", "Convierte a minúsculas."),
        new("ESPACIOS", "ESPACIOS(texto)", "Texto", "Quita espacios sobrantes."),
        new("TEXTO", "TEXTO(valor; \"dd/mm/aaaa\")", "Texto", "Da formato a un número o fecha."),
        new("VALOR", "VALOR(texto)", "Texto", "Convierte texto a número."),
        new("CONTIENE", "CONTIENE(texto; buscado)", "Texto", "VERDADERO si el texto contiene lo buscado."),
    };

    // Evalúa la fórmula. "referencia" resuelve [Nombre] → valor tipado (double, string, bool, DateTime o null).
    public static object? Evaluar(string formula, Func<string, object?> referencia)
    {
        var f = (formula ?? "").Trim();
        if (f.StartsWith('=')) f = f[1..];
        if (f.Length == 0) return null;
        var p = new Parser(Tokenizar(f), referencia);
        var r = p.Expresion();
        if (!p.Fin) throw new ErrorFormula($"Sobra texto cerca de \"{p.Actual.Texto}\".");
        return r;
    }

    // Nombres de campo referenciados en la fórmula ([...]).
    public static List<string> Referencias(string? formula)
    {
        var l = new List<string>();
        if (string.IsNullOrEmpty(formula)) return l;
        int i = 0;
        while ((i = formula.IndexOf('[', i)) >= 0)
        {
            int j = formula.IndexOf(']', i + 1);
            if (j < 0) break;
            l.Add(formula.Substring(i + 1, j - i - 1).Trim());
            i = j + 1;
        }
        return l;
    }

    // Cambia [Viejo] por [Nuevo] (al renombrar un campo).
    public static string Renombrar(string formula, string viejo, string nuevo)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < formula.Length)
        {
            if (formula[i] == '[')
            {
                int j = formula.IndexOf(']', i + 1);
                if (j > i && string.Equals(formula.Substring(i + 1, j - i - 1).Trim(), viejo, StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append('[').Append(nuevo).Append(']');
                    i = j + 1;
                    continue;
                }
            }
            sb.Append(formula[i]);
            i++;
        }
        return sb.ToString();
    }

    // ---------------- Conversión de valores ----------------

    public static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static bool EsVacio(object? v) => v is null || (v is string s && s.Length == 0);

    public static double ANumero(object? v) => v switch
    {
        null => 0,
        double d => d,
        bool b => b ? 1 : 0,
        DateTime f => f.ToOADate(),
        string s when s.Length == 0 => 0,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        string s when double.TryParse(s, NumberStyles.Float, Co, out var d) => d,
        _ => throw new ErrorFormula($"\"{v}\" no es un número.")
    };

    public static DateTime AFecha(object? v) => v switch
    {
        DateTime f => f,
        double d => DateTime.FromOADate(d),
        string s when DateTime.TryParse(s, Co, DateTimeStyles.None, out var f) => f,
        string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) => f,
        _ => throw new ErrorFormula($"\"{v}\" no es una fecha.")
    };

    public static bool ABool(object? v) => v switch
    {
        null => false,
        bool b => b,
        double d => d != 0,
        string s => s.Equals("VERDADERO", StringComparison.OrdinalIgnoreCase) || s.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("SI", StringComparison.OrdinalIgnoreCase) || s.Equals("SÍ", StringComparison.OrdinalIgnoreCase),
        DateTime => true,
        _ => false
    };

    public static string ATexto(object? v) => v switch
    {
        null => "",
        string s => s,
        bool b => b ? "VERDADERO" : "FALSO",
        double d => d.ToString("0.##########", Co),
        DateTime f => f.TimeOfDay == TimeSpan.Zero ? f.ToString("dd/MM/yyyy", Co) : f.ToString("dd/MM/yyyy HH:mm", Co),
        _ => v.ToString() ?? ""
    };

    // Compara como Excel: números con números, fechas con fechas, textos sin distinguir mayúsculas.
    public static int Comparar(object? a, object? b)
    {
        if (EsVacio(a) && EsVacio(b)) return 0;
        if (a is DateTime || b is DateTime)
        {
            if (EsVacio(a)) return -1; if (EsVacio(b)) return 1;
            return AFecha(a).CompareTo(AFecha(b));
        }
        if ((a is double || a is bool || EsVacio(a)) && (b is double || b is bool || EsVacio(b)))
            return ANumero(a).CompareTo(ANumero(b));
        if (a is string sa && b is double && double.TryParse(sa, NumberStyles.Float, CultureInfo.InvariantCulture, out var na)) return na.CompareTo((double)b);
        if (b is string sb && a is double && double.TryParse(sb, NumberStyles.Float, CultureInfo.InvariantCulture, out var nb)) return ((double)a).CompareTo(nb);
        return string.Compare(ATexto(a), ATexto(b), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- Tokenizador ----------------

    private enum T { Num, Txt, Ref, Id, Op, Fin }
    private record Tok(T Tipo, string Texto, double Num = 0);

    private static List<Tok> Tokenizar(string s)
    {
        var l = new List<Tok>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                int j = i;
                while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '.')) j++;
                if (j < s.Length && (s[j] == 'e' || s[j] == 'E') && j + 1 < s.Length && (char.IsDigit(s[j + 1]) || s[j + 1] == '-' || s[j + 1] == '+'))
                {
                    j += 2;
                    while (j < s.Length && char.IsDigit(s[j])) j++;
                }
                var txt = s[i..j];
                if (!double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) throw new ErrorFormula($"Número no válido: {txt}");
                l.Add(new Tok(T.Num, txt, n)); i = j; continue;
            }
            if (c == '"')
            {
                var sb = new StringBuilder(); int j = i + 1;
                while (true)
                {
                    if (j >= s.Length) throw new ErrorFormula("Falta cerrar comillas.");
                    if (s[j] == '"') { if (j + 1 < s.Length && s[j + 1] == '"') { sb.Append('"'); j += 2; continue; } break; }
                    sb.Append(s[j]); j++;
                }
                l.Add(new Tok(T.Txt, sb.ToString())); i = j + 1; continue;
            }
            if (c == '[')
            {
                int j = s.IndexOf(']', i + 1);
                if (j < 0) throw new ErrorFormula("Falta cerrar ']' en una referencia a campo.");
                l.Add(new Tok(T.Ref, s.Substring(i + 1, j - i - 1).Trim())); i = j + 1; continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                int j = i;
                while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_' || s[j] == '.')) j++;
                l.Add(new Tok(T.Id, s[i..j])); i = j; continue;
            }
            string? op = null;
            if (i + 1 < s.Length)
            {
                var dos = s.Substring(i, 2);
                if (dos is "<=" or ">=" or "<>" or "==" or "!=") op = dos;
            }
            op ??= c switch
            {
                '+' or '-' or '*' or '/' or '^' or '&' or '=' or '<' or '>' or '(' or ')' or ';' or ',' or '%' => c.ToString(),
                _ => null
            };
            if (op is null) throw new ErrorFormula($"Carácter no válido: '{c}'.");
            l.Add(new Tok(T.Op, op)); i += op.Length;
        }
        l.Add(new Tok(T.Fin, ""));
        return l;
    }

    // ---------------- Parser / evaluador (descendente recursivo) ----------------

    private class Parser
    {
        private readonly List<Tok> t;
        private readonly Func<string, object?> refs;
        private int p;
        public Parser(List<Tok> tokens, Func<string, object?> referencia) { t = tokens; refs = referencia; }
        public Tok Actual => t[p];
        public bool Fin => t[p].Tipo == T.Fin;
        private bool EsOp(string o) => t[p].Tipo == T.Op && t[p].Texto == o;
        private void Esperar(string o)
        {
            if (!EsOp(o)) throw new ErrorFormula($"Se esperaba '{o}'" + (Fin ? " al final." : $" cerca de \"{Actual.Texto}\"."));
            p++;
        }

        // comparación (la de menor precedencia, como en Excel)
        public object? Expresion()
        {
            var a = Concat();
            while (t[p].Tipo == T.Op && t[p].Texto is "=" or "==" or "<>" or "!=" or "<" or ">" or "<=" or ">=")
            {
                var op = t[p++].Texto;
                var b = Concat();
                int c = Comparar(a, b);
                a = op switch
                {
                    "=" or "==" => c == 0,
                    "<>" or "!=" => c != 0,
                    "<" => c < 0,
                    ">" => c > 0,
                    "<=" => c <= 0,
                    _ => c >= 0
                };
            }
            return a;
        }

        private object? Concat()
        {
            var a = Suma();
            while (EsOp("&")) { p++; var b = Suma(); a = ATexto(a) + ATexto(b); }
            return a;
        }

        private object? Suma()
        {
            var a = Producto();
            while (EsOp("+") || EsOp("-"))
            {
                var op = t[p++].Texto;
                var b = Producto();
                if (a is DateTime fa && b is DateTime fb) { a = op == "-" ? (fa - fb).TotalDays : throw new ErrorFormula("No se pueden sumar dos fechas."); continue; }
                if (a is DateTime f1) { a = f1.AddDays(op == "+" ? ANumero(b) : -ANumero(b)); continue; }
                if (b is DateTime f2 && op == "+") { a = f2.AddDays(ANumero(a)); continue; }
                a = op == "+" ? ANumero(a) + ANumero(b) : ANumero(a) - ANumero(b);
            }
            return a;
        }

        private object? Producto()
        {
            var a = PotenciaE();
            while (EsOp("*") || EsOp("/"))
            {
                var op = t[p++].Texto;
                var b = PotenciaE();
                if (op == "*") a = ANumero(a) * ANumero(b);
                else { var d = ANumero(b); if (d == 0) throw new ErrorFormula("División por cero."); a = ANumero(a) / d; }
            }
            return a;
        }

        private object? PotenciaE()
        {
            var a = Unario();
            while (EsOp("^")) { p++; var b = Unario(); a = Math.Pow(ANumero(a), ANumero(b)); }
            return a;
        }

        private object? Unario()
        {
            if (EsOp("-")) { p++; return -ANumero(Unario()); }
            if (EsOp("+")) { p++; return ANumero(Unario()); }
            var v = Primario();
            if (EsOp("%")) { p++; v = ANumero(v) / 100; }
            return v;
        }

        private object? Primario()
        {
            var k = t[p];
            switch (k.Tipo)
            {
                case T.Num: p++; return k.Num;
                case T.Txt: p++; return k.Texto;
                case T.Ref: p++; return refs(k.Texto);
                case T.Op when k.Texto == "(":
                    p++; var v = Expresion(); Esperar(")"); return v;
                case T.Id:
                    p++;
                    var nombre = k.Texto.ToUpperInvariant();
                    if (nombre is "VERDADERO" or "TRUE") return true;
                    if (nombre is "FALSO" or "FALSE") return false;
                    if (!EsOp("(")) throw new ErrorFormula($"\"{k.Texto}\" no se reconoce. Los campos van entre corchetes: [{k.Texto}].");
                    p++;
                    return Funcion(nombre);
                case T.Fin: throw new ErrorFormula("La fórmula está incompleta.");
                default: throw new ErrorFormula($"No se esperaba \"{k.Texto}\".");
            }
        }

        // Los argumentos se evalúan de forma perezosa para que SI y SI.ERROR no evalúen la rama descartada.
        private List<Func<object?>> Argumentos()
        {
            var args = new List<Func<object?>>();
            if (EsOp(")")) { p++; return args; }
            while (true)
            {
                int inicio = p;
                Saltar();
                int fin = p;
                var sub = t.GetRange(inicio, fin - inicio);
                sub.Add(new Tok(T.Fin, ""));
                args.Add(() =>
                {
                    var ps = new Parser(sub, refs);
                    if (ps.Fin) return null;
                    var r = ps.Expresion();
                    if (!ps.Fin) throw new ErrorFormula($"Sobra texto cerca de \"{ps.Actual.Texto}\".");
                    return r;
                });
                if (EsOp(";") || EsOp(",")) { p++; continue; }
                Esperar(")");
                return args;
            }
        }

        // Avanza hasta el siguiente separador o ')' del mismo nivel.
        private void Saltar()
        {
            int nivel = 0;
            while (!Fin)
            {
                if (EsOp("(")) nivel++;
                else if (EsOp(")")) { if (nivel == 0) return; nivel--; }
                else if ((EsOp(";") || EsOp(",")) && nivel == 0) return;
                p++;
            }
            throw new ErrorFormula("Falta cerrar ')'.");
        }

        private object? Funcion(string n)
        {
            var a = Argumentos();
            object? A(int i) => i < a.Count ? a[i]() : null;
            double N(int i) => ANumero(A(i));
            void Min(int c) { if (a.Count < c) throw new ErrorFormula($"{n} necesita al menos {c} argumento(s)."); }
            IEnumerable<double> Nums() => a.Select(f => f()).Where(v => !EsVacio(v)).Select(ANumero);

            switch (n)
            {
                case "SI": case "IF": Min(2); return ABool(A(0)) ? A(1) : (a.Count > 2 ? A(2) : false);
                case "Y": case "AND": Min(1); return a.All(f => ABool(f()));
                case "O": case "OR": Min(1); return a.Any(f => ABool(f()));
                case "NO": case "NOT": Min(1); return !ABool(A(0));
                case "SI.ERROR": case "SIERROR": case "IFERROR":
                    Min(2);
                    try { return A(0); } catch (ErrorFormula) { return A(1); }
                case "ESBLANCO": case "ISBLANK": Min(1); return EsVacio(A(0));
                case "HOY": case "TODAY": return DateTime.Today;
                case "AHORA": case "NOW": return DateTime.Now;
                case "FECHA": case "DATE": Min(3); return new DateTime((int)N(0), 1, 1).AddMonths((int)N(1) - 1).AddDays((int)N(2) - 1);
                case "DIAS": case "DAYS": Min(2); return Math.Floor((AFecha(A(0)).Date - AFecha(A(1)).Date).TotalDays);
                case "FECHA.MES": case "EDATE": Min(2); return AFecha(A(0)).AddMonths((int)N(1));
                case "AÑO": case "ANO": case "YEAR": Min(1); return (double)AFecha(A(0)).Year;
                case "MES": case "MONTH": Min(1); return (double)AFecha(A(0)).Month;
                case "DIA": case "DAY": Min(1); return (double)AFecha(A(0)).Day;
                case "DIASEM": case "WEEKDAY": Min(1); return (double)((int)AFecha(A(0)).DayOfWeek + 1);
                case "SUMA": case "SUM": return Nums().Sum();
                case "PROMEDIO": case "AVERAGE": { var l = Nums().ToList(); if (l.Count == 0) throw new ErrorFormula("PROMEDIO sin valores."); return l.Average(); }
                case "MIN": { var l = Nums().ToList(); return l.Count == 0 ? 0d : l.Min(); }
                case "MAX": { var l = Nums().ToList(); return l.Count == 0 ? 0d : l.Max(); }
                case "REDONDEAR": case "ROUND": Min(1); return Math.Round(N(0), (int)N(1), MidpointRounding.AwayFromZero);
                case "ABS": Min(1); return Math.Abs(N(0));
                case "ENTERO": case "INT": Min(1); return Math.Floor(N(0));
                case "RESIDUO": case "MOD": Min(2); { var d = N(1); if (d == 0) throw new ErrorFormula("División por cero."); var x = N(0); return x - d * Math.Floor(x / d); }
                case "POTENCIA": case "POWER": Min(2); return Math.Pow(N(0), N(1));
                case "RAIZ": case "SQRT": Min(1); { var x = N(0); if (x < 0) throw new ErrorFormula("Raíz de un número negativo."); return Math.Sqrt(x); }
                case "PI": return Math.PI;
                case "CONCATENAR": case "CONCAT": return string.Concat(a.Select(f => ATexto(f())));
                case "IZQUIERDA": case "LEFT": { Min(1); var s = ATexto(A(0)); var k = a.Count > 1 ? (int)N(1) : 1; return s[..Math.Clamp(k, 0, s.Length)]; }
                case "DERECHA": case "RIGHT": { Min(1); var s = ATexto(A(0)); var k = a.Count > 1 ? (int)N(1) : 1; k = Math.Clamp(k, 0, s.Length); return s[(s.Length - k)..]; }
                case "EXTRAE": case "MID": { Min(3); var s = ATexto(A(0)); var i0 = Math.Max(1, (int)N(1)) - 1; if (i0 >= s.Length) return ""; var k = Math.Clamp((int)N(2), 0, s.Length - i0); return s.Substring(i0, k); }
                case "LARGO": case "LEN": Min(1); return (double)ATexto(A(0)).Length;
                case "MAYUSC": case "UPPER": Min(1); return ATexto(A(0)).ToUpper(Co);
                case "MINUSC": case "LOWER": Min(1); return ATexto(A(0)).ToLower(Co);
                case "ESPACIOS": case "TRIM": Min(1); return string.Join(' ', ATexto(A(0)).Split(' ', StringSplitOptions.RemoveEmptyEntries));
                case "VALOR": case "VALUE": Min(1); return ANumero(A(0));
                case "CONTIENE": Min(2); return ATexto(A(0)).Contains(ATexto(A(1)), StringComparison.OrdinalIgnoreCase);
                case "TEXTO": case "TEXT":
                {
                    Min(2);
                    var v = A(0); var fmt = ATexto(A(1));
                    if (v is DateTime f) return f.ToString(FormatoFechaNet(fmt), Co);
                    if (EsVacio(v)) return "";
                    return ANumero(v).ToString(fmt.Replace(',', '.'), CultureInfo.InvariantCulture);
                }
                default: throw new ErrorFormula($"La función {n} no existe.");
            }
        }

        // "dd/mm/aaaa" (Excel en español) → "dd/MM/yyyy" (.NET).
        private static string FormatoFechaNet(string f)
        {
            var s = f.Replace("aaaa", "yyyy").Replace("aa", "yy").Replace("AAAA", "yyyy");
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                // "mm" es mes salvo si va justo después de "h" (minutos), como en Excel.
                if ((s[i] == 'm' || s[i] == 'M') && !(i > 0 && (s[i - 1] == 'h' || s[i - 1] == 'H' || s[i - 1] == ':'))) sb.Append('M');
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
