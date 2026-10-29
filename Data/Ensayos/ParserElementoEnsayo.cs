using System.Text.RegularExpressions;

namespace SigWeb.Data.Ensayos;

// Interpreta el texto de la columna "Elemento" de los ensayos de
// concreto (ej. "B", "BC", "ABCD", "B (PED)", "B (ZP)", "CUNETA M1").
// Reglas:
//   - Una o más letras seguidas (sin espacios), y CADA UNA de ellas es
//     el nombre de una pata real de esa torre = las patas afectadas
//     (una misma muestra puede cubrir varias, ej. "BC" = patas B y C).
//   - Sufijo "(PED)" = Pedestal de esas patas; "(ZP)" = Zapata; sin
//     sufijo = vaciado general (Ninguno).
//   - Cualquier otra cosa se guarda tal cual como "Otro", sin patas
//     asociadas (ej. "CUNETA", "CUNETA M1"): no se cruza con ninguna
//     pata en Trazado. Importante: hay que comparar contra las patas
//     reales de la torre (normalmente A-D) y no solo "¿son letras?",
//     porque "CUNETA" también son puras letras (C, U, N, E, T, A) y
//     sin esa validación se leería como si fueran 6 patas.
public static class ParserElementoEnsayo
{
    public class ResultadoParseo
    {
        public List<string> Patas { get; set; } = new();
        public SigWeb.Models.SubElementoResultado SubElemento { get; set; } = SigWeb.Models.SubElementoResultado.Ninguno;

        // false cuando el texto no calzó con el patrón esperado (letras
        // de pata reales de la torre + sufijo opcional) y se guardó
        // como "Otro": conviene que la pantalla de importación lo
        // señale para revisión.
        public bool Reconocido { get; set; } = true;
    }

    private static readonly Regex PatronPatas = new(@"^\s*([A-Za-z]{1,8})\s*(?:\(\s*(PED|ZP)\s*\))?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Patas por defecto usadas cuando aún no se conoce la torre (ej. al
    // diligenciar el campo "Elemento" manualmente antes de elegir la
    // torre). Al importar desde Excel, siempre se debe llamar con las
    // patas reales de la torre ya resuelta (patasValidas).
    private static readonly string[] PatasPorDefecto = { "A", "B", "C", "D" };

    public static ResultadoParseo Parsear(string? textoElemento, IEnumerable<string>? patasValidas = null)
    {
        string texto = (textoElemento ?? "").Trim();
        var resultado = new ResultadoParseo();

        if (string.IsNullOrEmpty(texto))
        {
            resultado.Reconocido = false;
            return resultado;
        }

        var universoPatas = new HashSet<string>(
            (patasValidas?.Any() == true ? patasValidas : PatasPorDefecto).Select(p => p.Trim().ToUpperInvariant()),
            StringComparer.OrdinalIgnoreCase);

        var m = PatronPatas.Match(texto);
        if (m.Success)
        {
            string letras = m.Groups[1].Value.ToUpperInvariant();
            var patas = letras.Select(c => c.ToString()).Distinct().ToList();

            if (patas.All(p => universoPatas.Contains(p)))
            {
                resultado.Patas = patas;
                string sufijo = m.Groups[2].Success ? m.Groups[2].Value.ToUpperInvariant() : "";
                resultado.SubElemento = sufijo switch
                {
                    "PED" => SigWeb.Models.SubElementoResultado.Pedestal,
                    "ZP" => SigWeb.Models.SubElementoResultado.Zapata,
                    _ => SigWeb.Models.SubElementoResultado.Ninguno
                };
                return resultado;
            }
        }

        // No coincide con patas reales de la torre: elemento especial
        // no ligado a pata (ej. "CUNETA", "CUNETA M1").
        resultado.SubElemento = SigWeb.Models.SubElementoResultado.Otro;
        return resultado;
    }

    // El Componente de una pata predeterminada se llama "Pata A", "Pata
    // B", etc. (ver Trazado.razor / CrearComponentesPorDefecto), no solo
    // "A". Para comparar contra las letras que trae el Excel ("A", "BC",
    // "ACD"...) hay que reducir ese nombre al código corto de la pata.
    // Si el componente no sigue ese patrón (fue renombrado, o es un
    // elemento especial), se deja el nombre tal cual.
    public static string ObtenerCodigoPata(string nombreComponente)
    {
        string nombre = (nombreComponente ?? "").Trim();
        const string prefijo = "Pata ";
        return nombre.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)
            ? nombre.Substring(prefijo.Length).Trim()
            : nombre;
    }

    // Nombre sugerido para crear (o reutilizar) el Componente de Trazado
    // que representará un elemento especial (no-pata) como "CUNETA M1".
    // El usuario siempre puede cambiarlo antes de guardar/importar.
    public static string ProponerNombreElemento(string? textoElemento)
    {
        string texto = (textoElemento ?? "").Trim();
        if (texto.Length == 0) return "";
        var partes = texto.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", partes.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
    }
}
