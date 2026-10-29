using System.Globalization;
using SigWeb.Data.Trazado;
using SigWeb.Models;

namespace SigWeb.Data.Ensayos;

// A qué destino se mapeó una columna del Excel al cargar un Tipo de
// Ensayo. Las constantes de texto son las que se guardan en
// MapeoColumnaEnsayo.Destino.
public static class DestinoColumnaEnsayo
{
    public const string Ignorar = "IGNORAR";
    public const string Torre = "TORRE";
    public const string Localizacion = "LOCALIZACION";
    public const string Elemento = "ELEMENTO";
    public const string Informe = "INFORME";
    public const string DisenoMezcla = "DISENO_MEZCLA";
    public const string PrefijoCampo = "CAMPO:";

    public static string ParaCampo(string clave) => PrefijoCampo + clave;
    public static string? ClaveCampo(string destino) =>
        destino.StartsWith(PrefijoCampo, StringComparison.Ordinal) ? destino.Substring(PrefijoCampo.Length) : null;
}

// El resultado de interpretar una fila del Excel: lista para revisar en
// pantalla y, si el usuario confirma, para guardar como ResultadoEnsayo.
public class FilaImportadaEnsayo
{
    public int Fila { get; set; } // número de fila de Excel, para mostrarlo en advertencias
    public string? TorreTexto { get; set; }
    public int? ElementoTrazadoId { get; set; }

    // Solo para Tipos de Ensayo que no usan Torre (usan Localización en su
    // lugar, ej. cilindros de concreto): texto tal como viene del Excel,
    // normalizado a la grafía del catálogo "Localización" si coincide con
    // una ya existente (ver resolverLocalizacion en Procesar). Si esa
    // Localización está vinculada a una Torre del trazado, ElementoTrazadoId
    // (arriba) también queda resuelto, igual que si fuera una Torre directa.
    public string? LocalizacionTexto { get; set; }

    // Cuando TorreTexto no coincidió con ninguna torre (ni con un alias
    // ya aprendido): códigos existentes más parecidos, para que el
    // usuario confirme cuál es la correcta en vez de perder el dato.
    public List<string> TorresSugeridas { get; set; } = new();
    public string ElementoTexto { get; set; } = "";
    public List<string> Patas { get; set; } = new();
    public SubElementoResultado SubElemento { get; set; } = SubElementoResultado.Ninguno;

    // Solo aplica cuando SubElemento == Otro (ej. "CUNETA M1"): nombre
    // con el que se creará (o reutilizará) el Componente de esa torre en
    // Trazado. Se propone automáticamente a partir de ElementoTexto,
    // pero el usuario lo revisa/cambia en la pantalla de revisión antes
    // de confirmar, ya que dos filas con distinto texto pueden referirse
    // al mismo elemento físico (o viceversa).
    public string ElementoEspecialNombre { get; set; } = "";

    public string? InformeNumero { get; set; }
    public string? DisenoMezclaCodigo { get; set; }

    // Clave de CampoEnsayo -> valor final que se guardaría.
    public Dictionary<string, string> Valores { get; set; } = new();

    // Clave de CampoEnsayo (solo fórmulas presentes también como columna
    // del Excel) -> (valor que traía la hoja, valor recalculado), cuando
    // no coinciden. El usuario decide cuál de los dos se queda.
    public Dictionary<string, (string ValorExcel, string ValorCalculado)> Discrepancias { get; set; } = new();

    // Para las discrepancias: qué eligió el usuario ("excel" o
    // "calculado"). Por defecto, "calculado" (recomendado).
    public Dictionary<string, string> EleccionDiscrepancia { get; set; } = new();

    public List<string> Advertencias { get; set; } = new();
    public bool TieneProblemas => Advertencias.Count > 0;
}

// Convierte una tabla cruda de Excel (leída con LectorExcelTablaTorres,
// reutilizado también aquí) + un mapeo columna->destino + la plantilla
// de campos de un Tipo de Ensayo, en filas listas para revisar y
// guardar. No toca la base de datos: recibe como funciones lo poco que
// necesita consultar (resolver una torre por su código, y las patas
// reales de esa torre), para poder probarse sin una base de datos real.
public static class ProcesadorImportacionEnsayos
{
    public static List<FilaImportadaEnsayo> Procesar(
        TablaTorresCruda tabla,
        IReadOnlyDictionary<int, string> destinoPorPosicion, // posición en tabla.Columnas -> destino
        IReadOnlyList<CampoEnsayo> plantilla,
        Func<string, int?> resolverTorre,
        Func<int, List<string>> patasDeTorre,
        Func<string, List<string>>? sugerirTorres = null,
        Func<string, (string NombreCanonico, int? ElementoTrazadoId)>? resolverLocalizacion = null)
    {
        var resultado = new List<FilaImportadaEnsayo>();
        var camposPorClave = plantilla.ToDictionary(c => c.Clave, StringComparer.OrdinalIgnoreCase);

        for (int f = 0; f < tabla.Filas.Count; f++)
        {
            var filaCruda = tabla.Filas[f];
            var fila = new FilaImportadaEnsayo { Fila = f + 1 };
            var valoresCrudos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int pos = 0; pos < tabla.Columnas.Count; pos++)
            {
                if (!destinoPorPosicion.TryGetValue(pos, out var destino) || destino == DestinoColumnaEnsayo.Ignorar) continue;
                string valorCelda = pos < filaCruda.Length ? filaCruda[pos] : "";

                if (destino == DestinoColumnaEnsayo.Torre) fila.TorreTexto = valorCelda;
                else if (destino == DestinoColumnaEnsayo.Localizacion) fila.LocalizacionTexto = valorCelda;
                else if (destino == DestinoColumnaEnsayo.Elemento) fila.ElementoTexto = valorCelda;
                else if (destino == DestinoColumnaEnsayo.Informe) fila.InformeNumero = valorCelda;
                else if (destino == DestinoColumnaEnsayo.DisenoMezcla) fila.DisenoMezclaCodigo = valorCelda;
                else
                {
                    var clave = DestinoColumnaEnsayo.ClaveCampo(destino);
                    if (clave is not null) valoresCrudos[clave] = valorCelda;
                }
            }

            // ---------- Torre ----------
            fila.ElementoTrazadoId = string.IsNullOrWhiteSpace(fila.TorreTexto) ? null : resolverTorre(fila.TorreTexto.Trim());
            if (!string.IsNullOrWhiteSpace(fila.TorreTexto) && fila.ElementoTrazadoId is null)
            {
                fila.TorresSugeridas = sugerirTorres?.Invoke(fila.TorreTexto.Trim()) ?? new();
                fila.Advertencias.Add(fila.TorresSugeridas.Count > 0
                    ? $"Torre \"{fila.TorreTexto}\" no se encontró; ¿es alguna de estas?: {string.Join(", ", fila.TorresSugeridas)}"
                    : $"Torre \"{fila.TorreTexto}\" no se encontró en el trazado; el resultado se guarda sin torre asociada.");
            }

            // ---------- Localización (tipos sin Torre) ----------
            // Se normaliza a la grafía exacta del catálogo si ya existe
            // (igual que al guardar manualmente); si no existe todavía, se
            // deja el texto tal como viene (se agrega al catálogo al
            // confirmar la importación). Si esa Localización está
            // vinculada a una Torre del trazado, el resultado también
            // queda cruzado con ella — igual que un ensayo con Torre
            // propia — para que aparezca en Trazado.
            if (!string.IsNullOrWhiteSpace(fila.LocalizacionTexto) && resolverLocalizacion is not null)
            {
                var (nombreCanonico, idTorreDeLocalizacion) = resolverLocalizacion(fila.LocalizacionTexto.Trim());
                fila.LocalizacionTexto = nombreCanonico;
                if (idTorreDeLocalizacion is not null) fila.ElementoTrazadoId = idTorreDeLocalizacion;
            }

            // ---------- Elemento (patas) ----------
            var patasValidas = fila.ElementoTrazadoId is int idTorre ? patasDeTorre(idTorre) : null;
            var parseo = ParserElementoEnsayo.Parsear(fila.ElementoTexto, patasValidas);
            fila.Patas = parseo.Patas;
            fila.SubElemento = parseo.SubElemento;
            if (parseo.SubElemento == SubElementoResultado.Otro && !string.IsNullOrWhiteSpace(fila.ElementoTexto))
            {
                fila.ElementoEspecialNombre = ParserElementoEnsayo.ProponerNombreElemento(fila.ElementoTexto);
                fila.Advertencias.Add(fila.ElementoTrazadoId is null
                    ? $"\"{fila.ElementoTexto}\" no se reconoce como pata; falta la torre para poder crearlo como elemento en Trazado."
                    : $"\"{fila.ElementoTexto}\" no se reconoce como pata; revisa el nombre con el que se creará como elemento de esa torre en Trazado.");
            }

            // ---------- Campos no-fórmula: tal cual, normalizados ----------
            var valoresFinal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var campo in plantilla.Where(c => c.TipoDato != TipoDatoCampoEnsayo.Formula))
            {
                if (valoresCrudos.TryGetValue(campo.Clave, out var crudo) && !string.IsNullOrWhiteSpace(crudo))
                {
                    valoresFinal[campo.Clave] = NormalizarValor(campo, crudo, fila);
                }
            }

            // ---------- Campos fórmula: se calculan, y se comparan contra
            // lo que traiga el Excel si esa columna también se mapeó ----------
            foreach (var campo in plantilla.Where(c => c.TipoDato == TipoDatoCampoEnsayo.Formula && !string.IsNullOrWhiteSpace(c.Formula)))
            {
                string calculado;
                try
                {
                    calculado = EvaluadorFormulas.Evaluar(campo.Formula!, valoresFinal!);
                }
                catch (Exception ex)
                {
                    fila.Advertencias.Add($"No se pudo calcular \"{campo.Etiqueta}\": {ex.Message}");
                    continue;
                }

                valoresFinal[campo.Clave] = calculado;

                if (valoresCrudos.TryGetValue(campo.Clave, out var valorExcel) && !string.IsNullOrWhiteSpace(valorExcel))
                {
                    if (!ValoresEquivalentes(valorExcel, calculado))
                    {
                        fila.Discrepancias[campo.Clave] = (valorExcel, calculado);
                        fila.EleccionDiscrepancia[campo.Clave] = "calculado"; // recomendado por defecto
                    }
                }
            }

            fila.Valores = valoresFinal;
            resultado.Add(fila);
        }

        return resultado;
    }

    // Aplica la elección del usuario sobre cada discrepancia (Excel vs.
    // recalculado) a Valores, dejando el diccionario listo para guardar.
    public static void AplicarElecciones(FilaImportadaEnsayo fila)
    {
        foreach (var (clave, (valorExcel, valorCalculado)) in fila.Discrepancias)
        {
            var eleccion = fila.EleccionDiscrepancia.TryGetValue(clave, out var e) ? e : "calculado";
            fila.Valores[clave] = eleccion == "excel" ? valorExcel : valorCalculado;
        }
    }

    private static string NormalizarValor(CampoEnsayo campo, string crudo, FilaImportadaEnsayo fila)
    {
        crudo = crudo.Trim();
        if (campo.TipoDato == TipoDatoCampoEnsayo.Numero)
        {
            if (double.TryParse(crudo, NumberStyles.Any, CultureInfo.InvariantCulture, out var numero))
                return numero.ToString("0.######", CultureInfo.InvariantCulture);
            fila.Advertencias.Add($"\"{campo.Etiqueta}\": \"{crudo}\" no es un número; se guardó tal cual.");
            return crudo;
        }
        if (campo.TipoDato == TipoDatoCampoEnsayo.Fecha)
        {
            string[] formatos = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "M/d/yyyy", "dd-MM-yyyy" };
            if (DateTime.TryParseExact(crudo, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
                || DateTime.TryParse(crudo, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
                || DateTime.TryParse(crudo, CultureInfo.GetCultureInfo("es-CO"), DateTimeStyles.None, out fecha))
            {
                return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            fila.Advertencias.Add($"\"{campo.Etiqueta}\": \"{crudo}\" no se reconoce como fecha; se guardó tal cual.");
            return crudo;
        }
        return crudo;
    }

    private static bool ValoresEquivalentes(string a, string b)
    {
        if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out var da) &&
            double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out var db))
        {
            double diferencia = Math.Abs(da - db);
            double escala = Math.Max(1, Math.Max(Math.Abs(da), Math.Abs(db)));
            return diferencia <= escala * 0.005; // 0.5% de tolerancia (redondeos)
        }
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
