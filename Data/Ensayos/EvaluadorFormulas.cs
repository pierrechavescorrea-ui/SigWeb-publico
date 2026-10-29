using System.Globalization;

namespace SigWeb.Data.Ensayos;

// Evaluador de las fórmulas que el usuario escribe en un CampoEnsayo
// (ej. "{CargaPsi} / {FcDiseno}", o una regla condicional con IIF). Es
// intencionalmente propio y liviano (sin librería externa) para no
// depender de un paquete NuGet que este entorno no puede verificar que
// compile: solo soporta lo que estas fórmulas necesitan.
//
// Soporta: números, texto entre comillas, true/false, +  -  *  /  ^,
// comparaciones (== != < > <= >=), && ||, paréntesis, y las funciones
// PI(), ABS(x), REDONDEAR(x, n) e IIF(condición, siValor, noValor).
// Las referencias a otros campos van como {Clave} y se reemplazan por
// su valor (numérico si es posible, texto entre comillas si no) antes
// de evaluar la expresión.
public static class EvaluadorFormulas
{
    public class ErrorFormula : Exception
    {
        public ErrorFormula(string mensaje) : base(mensaje) { }
    }

    // Reemplaza cada "{Clave}" por el valor correspondiente de
    // "valores" y evalúa el resultado. Devuelve texto listo para
    // guardar en ValorCampoEnsayo.Valor (números en formato invariante,
    // sin ceros de más).
    public static string Evaluar(string formula, IReadOnlyDictionary<string, string?> valores)
    {
        string sustituida = SustituirCampos(formula, valores);
        object? resultado = new Parser(sustituida).ParsearExpresionCompleta();
        return FormatearResultado(resultado);
    }

    private static string SustituirCampos(string formula, IReadOnlyDictionary<string, string?> valores)
    {
        var salida = new System.Text.StringBuilder();
        int i = 0;
        while (i < formula.Length)
        {
            if (formula[i] == '{')
            {
                int fin = formula.IndexOf('}', i + 1);
                if (fin < 0) throw new ErrorFormula("Falta cerrar '}' en la fórmula.");
                string clave = formula.Substring(i + 1, fin - i - 1).Trim();
                valores.TryGetValue(clave, out var valor);
                salida.Append(LiteralPara(valor));
                i = fin + 1;
            }
            else
            {
                salida.Append(formula[i]);
                i++;
            }
        }
        return salida.ToString();
    }

    private static string LiteralPara(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return "\"\"";
        if (double.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var numero))
        {
            return numero.ToString("R", CultureInfo.InvariantCulture);
        }
        return "\"" + valor.Replace("\"", "'") + "\"";
    }

    private static string FormatearResultado(object? valor)
    {
        return valor switch
        {
            null => "",
            bool b => b ? "Sí" : "No",
            double d => Math.Round(d, 6, MidpointRounding.AwayFromZero)
                .ToString("0.######", CultureInfo.InvariantCulture),
            _ => valor.ToString() ?? ""
        };
    }

    // ---------------- Tokenizer + parser recursivo descendente ----------------

    private enum TipoToken { Numero, Texto, Identificador, Simbolo, Fin }

    private struct Token
    {
        public TipoToken Tipo;
        public string Texto;
        public double NumeroValor;
    }

    private class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public Parser(string expresion)
        {
            _tokens = Tokenizar(expresion);
            _pos = 0;
        }

        private Token Actual => _tokens[_pos];

        private static List<Token> Tokenizar(string s)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                {
                    int inicio = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    string numTexto = s.Substring(inicio, i - inicio);
                    tokens.Add(new Token { Tipo = TipoToken.Numero, NumeroValor = double.Parse(numTexto, CultureInfo.InvariantCulture), Texto = numTexto });
                    continue;
                }

                if (c == '"')
                {
                    int inicio = i + 1;
                    int fin = s.IndexOf('"', inicio);
                    if (fin < 0) throw new ErrorFormula("Falta cerrar comillas en la fórmula.");
                    tokens.Add(new Token { Tipo = TipoToken.Texto, Texto = s.Substring(inicio, fin - inicio) });
                    i = fin + 1;
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int inicio = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    tokens.Add(new Token { Tipo = TipoToken.Identificador, Texto = s.Substring(inicio, i - inicio) });
                    continue;
                }

                // operadores de 2 caracteres
                if (i + 1 < s.Length)
                {
                    string dos = s.Substring(i, 2);
                    if (dos is "==" or "!=" or "<=" or ">=" or "&&" or "||")
                    {
                        tokens.Add(new Token { Tipo = TipoToken.Simbolo, Texto = dos });
                        i += 2;
                        continue;
                    }
                }

                if ("+-*/^()<>,".IndexOf(c) >= 0)
                {
                    tokens.Add(new Token { Tipo = TipoToken.Simbolo, Texto = c.ToString() });
                    i++;
                    continue;
                }

                throw new ErrorFormula($"Carácter no reconocido en la fórmula: '{c}'");
            }
            tokens.Add(new Token { Tipo = TipoToken.Fin, Texto = "" });
            return tokens;
        }

        public object? ParsearExpresionCompleta()
        {
            var resultado = ParsearOr();
            if (Actual.Tipo != TipoToken.Fin) throw new ErrorFormula($"Sobra texto en la fórmula cerca de '{Actual.Texto}'.");
            return resultado;
        }

        private object? ParsearOr()
        {
            var izq = ParsearAnd();
            while (Actual.Tipo == TipoToken.Simbolo && Actual.Texto == "||")
            {
                _pos++;
                var der = ParsearAnd();
                izq = ABoolean(izq) || ABoolean(der);
            }
            return izq;
        }

        private object? ParsearAnd()
        {
            var izq = ParsearComparacion();
            while (Actual.Tipo == TipoToken.Simbolo && Actual.Texto == "&&")
            {
                _pos++;
                var der = ParsearComparacion();
                izq = ABoolean(izq) && ABoolean(der);
            }
            return izq;
        }

        private object? ParsearComparacion()
        {
            var izq = ParsearSuma();
            if (Actual.Tipo == TipoToken.Simbolo && Actual.Texto is "==" or "!=" or "<" or ">" or "<=" or ">=")
            {
                string op = Actual.Texto;
                _pos++;
                var der = ParsearSuma();
                return Comparar(izq, der, op);
            }
            return izq;
        }

        private object? ParsearSuma()
        {
            var izq = ParsearMultiplicacion();
            while (Actual.Tipo == TipoToken.Simbolo && (Actual.Texto == "+" || Actual.Texto == "-"))
            {
                string op = Actual.Texto;
                _pos++;
                var der = ParsearMultiplicacion();
                izq = op == "+" ? ANumero(izq) + ANumero(der) : ANumero(izq) - ANumero(der);
            }
            return izq;
        }

        private object? ParsearMultiplicacion()
        {
            var izq = ParsearPotencia();
            while (Actual.Tipo == TipoToken.Simbolo && (Actual.Texto == "*" || Actual.Texto == "/"))
            {
                string op = Actual.Texto;
                _pos++;
                var der = ParsearPotencia();
                if (op == "*") { izq = ANumero(izq) * ANumero(der); }
                else
                {
                    double divisor = ANumero(der);
                    izq = divisor == 0 ? throw new ErrorFormula("División por cero.") : ANumero(izq) / divisor;
                }
            }
            return izq;
        }

        private object? ParsearPotencia()
        {
            var izq = ParsearUnario();
            if (Actual.Tipo == TipoToken.Simbolo && Actual.Texto == "^")
            {
                _pos++;
                var der = ParsearPotencia(); // asociativo a la derecha
                return Math.Pow(ANumero(izq), ANumero(der));
            }
            return izq;
        }

        private object? ParsearUnario()
        {
            if (Actual.Tipo == TipoToken.Simbolo && Actual.Texto == "-")
            {
                _pos++;
                return -ANumero(ParsearUnario());
            }
            return ParsearPrimario();
        }

        private object? ParsearPrimario()
        {
            var t = Actual;
            switch (t.Tipo)
            {
                case TipoToken.Numero:
                    _pos++;
                    return t.NumeroValor;
                case TipoToken.Texto:
                    _pos++;
                    return t.Texto;
                case TipoToken.Identificador:
                    return ParsearIdentificadorOFuncion();
                case TipoToken.Simbolo when t.Texto == "(":
                    _pos++;
                    var interno = ParsearOr();
                    Esperar(")");
                    return interno;
                default:
                    throw new ErrorFormula($"Se esperaba un valor en la fórmula cerca de '{t.Texto}'.");
            }
        }

        private object? ParsearIdentificadorOFuncion()
        {
            string nombre = Actual.Texto.ToUpperInvariant();
            _pos++;

            if (nombre == "TRUE") return true;
            if (nombre == "FALSE") return false;

            if (Actual.Tipo == TipoToken.Simbolo && Actual.Texto == "(")
            {
                _pos++;
                var argumentos = new List<object?>();
                if (!(Actual.Tipo == TipoToken.Simbolo && Actual.Texto == ")"))
                {
                    argumentos.Add(ParsearOr());
                    while (Actual.Tipo == TipoToken.Simbolo && Actual.Texto == ",")
                    {
                        _pos++;
                        argumentos.Add(ParsearOr());
                    }
                }
                Esperar(")");
                return EjecutarFuncion(nombre, argumentos);
            }

            throw new ErrorFormula($"'{nombre}' no es un campo ni una función reconocida.");
        }

        private static object? EjecutarFuncion(string nombre, List<object?> args)
        {
            switch (nombre)
            {
                case "PI":
                    return Math.PI;
                case "ABS":
                    Requiere(args, 1, nombre);
                    return Math.Abs(ANumero(args[0]));
                case "REDONDEAR":
                case "ROUND":
                    Requiere(args, 2, nombre);
                    return Math.Round(ANumero(args[0]), (int)ANumero(args[1]), MidpointRounding.AwayFromZero);
                case "IIF":
                case "IF":
                case "SI":
                    Requiere(args, 3, nombre);
                    return ABoolean(args[0]) ? args[1] : args[2];
                default:
                    throw new ErrorFormula($"Función no reconocida: {nombre}().");
            }
        }

        private static void Requiere(List<object?> args, int cantidad, string nombre)
        {
            if (args.Count != cantidad) throw new ErrorFormula($"{nombre}() necesita {cantidad} argumento(s).");
        }

        private void Esperar(string simbolo)
        {
            if (Actual.Tipo != TipoToken.Simbolo || Actual.Texto != simbolo)
                throw new ErrorFormula($"Se esperaba '{simbolo}' en la fórmula cerca de '{Actual.Texto}'.");
            _pos++;
        }

        private static bool Comparar(object? izq, object? der, string op)
        {
            if (izq is double || der is double)
            {
                double a = ANumero(izq), b = ANumero(der);
                return op switch
                {
                    "==" => a == b,
                    "!=" => a != b,
                    "<" => a < b,
                    ">" => a > b,
                    "<=" => a <= b,
                    ">=" => a >= b,
                    _ => throw new ErrorFormula($"Operador no reconocido: {op}")
                };
            }
            string sa = izq?.ToString() ?? "", sb = der?.ToString() ?? "";
            return op switch
            {
                "==" => sa == sb,
                "!=" => sa != sb,
                _ => throw new ErrorFormula($"'{op}' no se puede usar para comparar texto.")
            };
        }

        private static double ANumero(object? valor)
        {
            return valor switch
            {
                double d => d,
                bool b => b ? 1 : 0,
                string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) => n,
                null => throw new ErrorFormula("Se esperaba un número en la fórmula."),
                _ => throw new ErrorFormula($"'{valor}' no es un número válido en la fórmula.")
            };
        }

        private static bool ABoolean(object? valor)
        {
            return valor switch
            {
                bool b => b,
                double d => d != 0,
                string s => !string.IsNullOrEmpty(s),
                _ => false
            };
        }
    }
}
