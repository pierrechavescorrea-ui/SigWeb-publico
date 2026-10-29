using System.Globalization;
using SigWeb.Models;

namespace SigWeb.Data.Tablas;

// Columna propia de una tabla (la página la declara): clave = propiedad, cómo leer su valor por fila.
public class ColumnaNativa
{
    public ColumnaNativa(string clave, string nombre, TipoCampoTabla tipo, Func<int, object?> valor, bool visiblePorDefecto = true, string? origen = null)
    { Clave = clave; Nombre = nombre; Tipo = tipo; Valor = valor; VisiblePorDefecto = visiblePorDefecto; Origen = origen; }
    public bool VisiblePorDefecto { get; }
    // Etiqueta de origen para el panel Campos cuando la columna viene de otro sistema (ej. "Excel" en Equipos/Ensayos).
    public string? Origen { get; }
    public string Clave { get; }
    public string Nombre { get; }
    public TipoCampoTabla Tipo { get; }
    public Func<int, object?> Valor { get; }
}

// Estilo resuelto de una celda por formato condicional.
public record EstiloCelda(string Forma, string ColorTexto, string ColorFondo, bool Negrita);

// Estado en memoria de los campos de UNA tabla: valores, cálculo de fórmulas y
// formatos condicionales. La carga y el guardado van en MotorTabla.Datos.cs.
public partial class MotorTabla
{
    public string Tabla { get; }
    public List<CampoTabla> Campos { get; set; } = new();
    public Dictionary<string, ColumnaNativa> Nativas { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<(int Campo, int Fila), ValorCampoTabla> Valores { get; set; } = new();
    public Dictionary<int, List<FormatoCondicional>> Formatos { get; set; } = new();
    public Dictionary<string, List<string>> OpcionesCatalogo { get; set; } = new();
    // Filas actuales de la tabla (id → etiqueta) para el asistente de fórmulas.
    public List<(int Id, string Etiqueta)> Filas { get; set; } = new();

    public MotorTabla(string tabla) { Tabla = tabla; }

    public string EtiquetaOrigen(CampoTabla c) => c.Origen switch
    {
        OrigenCampoTabla.Nativo => Nativas.TryGetValue(c.Clave, out var n) && n.Origen is not null ? n.Origen : "Propio",
        OrigenCampoTabla.Excel => "Excel",
        _ => "Creado"
    };

    public IEnumerable<CampoTabla> Visibles => Campos.Where(c => c.Visible).OrderBy(c => c.Orden);
    public IEnumerable<CampoTabla> Ordenados => Campos.OrderBy(c => c.Orden);

    public CampoTabla? PorNombre(string nombre) =>
        Campos.FirstOrDefault(c => string.Equals(c.Nombre.Trim(), nombre.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? Campos.FirstOrDefault(c => string.Equals(c.Clave, nombre.Trim(), StringComparison.OrdinalIgnoreCase));

    public CampoTabla? PorClave(string clave) => Campos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public List<string> OpcionesDe(CampoTabla c)
    {
        if (c.Tipo != TipoCampoTabla.Lista) return new();
        if (!string.IsNullOrWhiteSpace(c.CatalogoTipo))
            return OpcionesCatalogo.TryGetValue(c.CatalogoTipo, out var l) ? l : new();
        return (c.Opciones ?? "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
    }

    // ---------- Valores ----------

    public ValorCampoTabla? Registro(int fila, CampoTabla c) => Valores.TryGetValue((c.Id, fila), out var v) ? v : null;

    // Fórmula que manda en esta celda: la propia de la celda, o la de la columna si el campo es calculado.
    public string? FormulaDe(int fila, CampoTabla c)
    {
        var r = Registro(fila, c);
        if (!string.IsNullOrWhiteSpace(r?.Formula)) return r!.Formula;
        return c.Calculado ? c.Formula : null;
    }

    public bool FormulaDeCelda(int fila, CampoTabla c) => !string.IsNullOrWhiteSpace(Registro(fila, c)?.Formula);

    // Valor tipado (double, string, bool, DateTime o null). Lanza FormulaExcel.ErrorFormula si la fórmula falla.
    public object? Valor(int fila, CampoTabla c) => Valor(fila, c, new HashSet<int>());

    private object? Valor(int fila, CampoTabla c, HashSet<int> pila)
    {
        if (c.Origen == OrigenCampoTabla.Nativo && Nativas.TryGetValue(c.Clave, out var nat))
            return Normalizar(nat.Valor(fila));
        var formula = FormulaDe(fila, c);
        if (formula is not null)
        {
            if (!pila.Add(c.Id)) throw new FormulaExcel.ErrorFormula($"Referencia circular en [{c.Nombre}].");
            try
            {
                var r = FormulaExcel.Evaluar(formula, nombre =>
                {
                    var otro = PorNombre(nombre) ?? throw new FormulaExcel.ErrorFormula($"No existe el campo [{nombre}].");
                    return Valor(fila, otro, pila);
                });
                // Como en Excel, un número en una columna de tipo fecha se muestra como fecha.
                if (c.Tipo == TipoCampoTabla.Fecha && r is double d) return DateTime.FromOADate(d);
                return r;
            }
            finally { pila.Remove(c.Id); }
        }
        return Tipar(c, Registro(fila, c)?.Valor);
    }

    // Prueba una fórmula sobre una fila (asistente), sin guardarla.
    public object? Probar(string formula, int fila) =>
        FormulaExcel.Evaluar(formula, nombre =>
        {
            var otro = PorNombre(nombre) ?? throw new FormulaExcel.ErrorFormula($"No existe el campo [{nombre}].");
            return Valor(fila, otro);
        });

    private static object? Normalizar(object? v) => v switch
    {
        null => null,
        int i => (double)i,
        long l => (double)l,
        decimal d => (double)d,
        float f => (double)f,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        string s => s.Length == 0 ? null : s,
        _ => v
    };

    // Texto guardado → valor tipado según el tipo del campo.
    public static object? Tipar(CampoTabla c, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        switch (c.Tipo)
        {
            case TipoCampoTabla.Numero:
                if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return n;
                if (double.TryParse(texto, NumberStyles.Float, FormulaExcel.Co, out n)) return n;
                return texto;
            case TipoCampoTabla.Fecha:
                if (DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)) return f;
                if (DateTime.TryParse(texto, FormulaExcel.Co, DateTimeStyles.None, out f)) return f;
                return texto;
            case TipoCampoTabla.SiNo:
                return texto is "1" or "true" or "True" or "Sí" or "SI" or "VERDADERO";
            case TipoCampoTabla.Auto:
                if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var na)) return na;
                if (DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fa)) return fa;
                return texto;
            default:
                return texto;
        }
    }

    // Valor tipado → texto para guardar (invariante; fechas yyyy-MM-dd).
    public static string? Serializar(object? v) => v switch
    {
        null => null,
        DateTime f => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        bool b => b ? "1" : "0",
        _ => v.ToString()
    };

    // Texto para mostrar en la celda.
    public string Texto(int fila, CampoTabla c, out string? error)
    {
        error = null;
        object? v;
        try { v = Valor(fila, c); }
        catch (FormulaExcel.ErrorFormula e) { error = e.Message; return "#ERROR"; }
        catch (Exception e) { error = e.Message; return "#ERROR"; }
        return Mostrar(c, v);
    }

    public static string Mostrar(CampoTabla c, object? v)
    {
        if (v is double d && c.Decimales is int k) return d.ToString("N" + k, FormulaExcel.Co);
        if (v is bool b) return b ? "Sí" : "No";
        return FormulaExcel.ATexto(v);
    }

    // ---------- Ayudas para las páginas ----------

    // Ordena filas por cualquier campo (propio o creado) usando el valor que muestra la tabla.
    public IEnumerable<T> Ordenar<T>(IEnumerable<T> filas, Func<T, int> id, string clave, bool ascendente)
    {
        var c = PorClave(clave);
        if (c is null) return filas;
        object? V(T f) { try { return Valor(id(f), c); } catch { return null; } }
        var cmp = Comparer<object?>.Create(FormulaExcel.Comparar);
        return ascendente ? filas.OrderBy(V, cmp) : filas.OrderByDescending(V, cmp);
    }

    // Texto de los campos creados/Excel visibles de una fila (para los buscadores).
    public string TextoBusqueda(int fila) =>
        string.Join(" ", Visibles.Where(c => c.Origen != OrigenCampoTabla.Nativo).Select(c => Texto(fila, c, out _)));

    // ---------- Formatos condicionales ----------

    public EstiloCelda? Estilo(int fila, CampoTabla c)
    {
        if (!Formatos.TryGetValue(c.Id, out var reglas) || reglas.Count == 0) return null;
        object? v;
        try { v = Valor(fila, c); } catch { return null; }
        foreach (var r in reglas.Where(x => x.Activo).OrderBy(x => x.Orden))
        {
            bool ok;
            try { ok = Cumple(r, v, fila); } catch { ok = false; }
            if (ok) return EstiloDe(r);
        }
        return null;
    }

    public bool Cumple(FormatoCondicional r, object? v, int fila)
    {
        object? V(string? t) => ValorRegla(v, t);
        switch (r.Operador)
        {
            case OperadorFormato.Vacio: return FormulaExcel.EsVacio(v);
            case OperadorFormato.NoVacio: return !FormulaExcel.EsVacio(v);
            case OperadorFormato.Igual: return FormulaExcel.Comparar(v, V(r.Valor1)) == 0;
            case OperadorFormato.Diferente: return FormulaExcel.Comparar(v, V(r.Valor1)) != 0;
            case OperadorFormato.Mayor: return !FormulaExcel.EsVacio(v) && FormulaExcel.Comparar(v, V(r.Valor1)) > 0;
            case OperadorFormato.MayorIgual: return !FormulaExcel.EsVacio(v) && FormulaExcel.Comparar(v, V(r.Valor1)) >= 0;
            case OperadorFormato.Menor: return !FormulaExcel.EsVacio(v) && FormulaExcel.Comparar(v, V(r.Valor1)) < 0;
            case OperadorFormato.MenorIgual: return !FormulaExcel.EsVacio(v) && FormulaExcel.Comparar(v, V(r.Valor1)) <= 0;
            case OperadorFormato.Entre:
            case OperadorFormato.NoEntre:
            {
                if (FormulaExcel.EsVacio(v)) return false;
                var dentro = FormulaExcel.Comparar(v, V(r.Valor1)) >= 0 && FormulaExcel.Comparar(v, V(r.Valor2)) <= 0;
                return r.Operador == OperadorFormato.Entre ? dentro : !dentro;
            }
            case OperadorFormato.Contiene: return FormulaExcel.ATexto(v).Contains(r.Valor1 ?? "", StringComparison.OrdinalIgnoreCase);
            case OperadorFormato.NoContiene: return !FormulaExcel.ATexto(v).Contains(r.Valor1 ?? "", StringComparison.OrdinalIgnoreCase);
            case OperadorFormato.EmpiezaCon: return FormulaExcel.ATexto(v).StartsWith(r.Valor1 ?? "", StringComparison.OrdinalIgnoreCase);
            case OperadorFormato.TerminaCon: return FormulaExcel.ATexto(v).EndsWith(r.Valor1 ?? "", StringComparison.OrdinalIgnoreCase);
            case OperadorFormato.FechaAntesDeHoy: return Fecha(v) is DateTime a && a.Date < DateTime.Today;
            case OperadorFormato.FechaHoy: return Fecha(v) is DateTime h && h.Date == DateTime.Today;
            case OperadorFormato.FechaDespuesDeHoy: return Fecha(v) is DateTime p && p.Date > DateTime.Today;
            case OperadorFormato.FechaProximosDias:
            {
                if (Fecha(v) is not DateTime f) return false;
                var n = Num(r.Valor1);
                return f.Date >= DateTime.Today && f.Date <= DateTime.Today.AddDays(n);
            }
            case OperadorFormato.FechaUltimosDias:
            {
                if (Fecha(v) is not DateTime f) return false;
                var n = Num(r.Valor1);
                return f.Date <= DateTime.Today && f.Date >= DateTime.Today.AddDays(-n);
            }
            case OperadorFormato.Duplicado: return !FormulaExcel.EsVacio(v) && Conteo(r.CampoTablaId, v) > 1;
            case OperadorFormato.Unico: return !FormulaExcel.EsVacio(v) && Conteo(r.CampoTablaId, v) == 1;
            case OperadorFormato.CodigoConEspacios: return SigWeb.Data.Comunicados.LogicaComunicados.TieneEspaciosIndebidos(FormulaExcel.ATexto(v));
            case OperadorFormato.Formula:
                // "Usar una fórmula": [Valor] es el valor de esta celda; también se pueden usar los demás campos de la fila.
                return FormulaExcel.ABool(FormulaExcel.Evaluar(r.Valor1 ?? "", nombre =>
                {
                    if (string.Equals(nombre, "Valor", StringComparison.OrdinalIgnoreCase)) return v;
                    var otro = PorNombre(nombre) ?? throw new FormulaExcel.ErrorFormula($"No existe el campo [{nombre}].");
                    return Valor(fila, otro);
                }));
            default: return false;
        }
    }

    // ---------- Duplicados: cuántas filas tienen el mismo valor en la columna ----------
    // Se compara sin mayúsculas, sin espacios sobrantes y sin espacios alrededor de los guiones (códigos).
    private Dictionary<int, Dictionary<string, int>> conteos = new();

    public void InvalidarConteos() => conteos.Clear();

    private static string ClaveConteo(object? v) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(FormulaExcel.ATexto(v).Trim().ToUpperInvariant(), @"\s*-\s*", "-"), @"\s+", " ");

    private int Conteo(int campoId, object? v)
    {
        if (!conteos.TryGetValue(campoId, out var d))
        {
            d = new Dictionary<string, int>();
            var c = Campos.FirstOrDefault(x => x.Id == campoId);
            if (c is not null)
                foreach (var (fila, _) in Filas)
                {
                    object? x;
                    try { x = Valor(fila, c); } catch { continue; }
                    if (FormulaExcel.EsVacio(x)) continue;
                    var k = ClaveConteo(x);
                    d[k] = d.TryGetValue(k, out var n) ? n + 1 : 1;
                }
            conteos[campoId] = d;
        }
        return d.TryGetValue(ClaveConteo(v), out var cuenta) ? cuenta : 0;
    }

    private static double Num(string? t) =>
        double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n
        : double.TryParse(t, NumberStyles.Float, FormulaExcel.Co, out n) ? n : 0;

    private static DateTime? Fecha(object? v)
    {
        if (v is DateTime f) return f;
        if (v is string s && DateTime.TryParse(s, FormulaExcel.Co, DateTimeStyles.None, out var d)) return d;
        return null;
    }

    // El valor escrito en la regla se interpreta como el tipo del valor de la celda (número, fecha o texto).
    private static object? ValorRegla(object? celda, string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        if (celda is DateTime)
        {
            if (DateTime.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)) return f;
            if (DateTime.TryParse(t, FormulaExcel.Co, DateTimeStyles.None, out f)) return f;
        }
        if (celda is double || celda is null)
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return n;
            if (double.TryParse(t, NumberStyles.Float, FormulaExcel.Co, out n)) return n;
        }
        return t;
    }

    // Paleta de estilos estándar: (clave, nombre, fondo, texto).
    public static readonly IReadOnlyList<(string Clave, string Nombre, string Fondo, string Texto)> Paleta = new[]
    {
        ("verde", "Verde", "#198754", "#ffffff"),
        ("rojo", "Rojo", "#dc3545", "#ffffff"),
        ("amarillo", "Amarillo", "#ffc107", "#1f1f1f"),
        ("naranja", "Naranja", "#fd7e14", "#ffffff"),
        ("azul", "Azul", "#0d6efd", "#ffffff"),
        ("cian", "Cian", "#0dcaf0", "#0b2a33"),
        ("violeta", "Violeta", "#6f42c1", "#ffffff"),
        ("rosado", "Rosado", "#d63384", "#ffffff"),
        ("gris", "Gris", "#6c757d", "#ffffff"),
        ("negro", "Negro", "#212529", "#ffffff"),
        ("verde-suave", "Verde suave", "#d1f2df", "#0f5132"),
        ("rojo-suave", "Rojo suave", "#f8d7da", "#842029"),
        ("amarillo-suave", "Amarillo suave", "#fff3cd", "#664d03"),
        ("azul-suave", "Azul suave", "#cfe2ff", "#084298"),
        ("violeta-suave", "Violeta suave", "#e2d9f3", "#3d2a6b"),
        ("gris-suave", "Gris suave", "#e9ecef", "#343a40"),
    };

    public static EstiloCelda EstiloDe(FormatoCondicional r)
    {
        if (r.Estilo != "personalizado")
        {
            var p = Paleta.FirstOrDefault(x => x.Clave == r.Estilo);
            if (p.Clave is not null)
                return new EstiloCelda(r.Forma, r.Forma == "texto" ? p.Fondo : p.Texto, p.Fondo, r.Negrita);
        }
        return new EstiloCelda(r.Forma, r.ColorTexto ?? "#212529", r.ColorFondo ?? "transparent", r.Negrita);
    }

    // Descripción legible de la regla (lista de reglas del panel Campos).
    public static string Describir(FormatoCondicional r) => r.Operador switch
    {
        OperadorFormato.Igual => $"es igual a {r.Valor1}",
        OperadorFormato.Diferente => $"es diferente de {r.Valor1}",
        OperadorFormato.Mayor => $"es mayor que {r.Valor1}",
        OperadorFormato.MayorIgual => $"es mayor o igual que {r.Valor1}",
        OperadorFormato.Menor => $"es menor que {r.Valor1}",
        OperadorFormato.MenorIgual => $"es menor o igual que {r.Valor1}",
        OperadorFormato.Entre => $"está entre {r.Valor1} y {r.Valor2}",
        OperadorFormato.NoEntre => $"no está entre {r.Valor1} y {r.Valor2}",
        OperadorFormato.Contiene => $"contiene «{r.Valor1}»",
        OperadorFormato.NoContiene => $"no contiene «{r.Valor1}»",
        OperadorFormato.EmpiezaCon => $"empieza con «{r.Valor1}»",
        OperadorFormato.TerminaCon => $"termina con «{r.Valor1}»",
        OperadorFormato.Vacio => "está vacío",
        OperadorFormato.NoVacio => "no está vacío",
        OperadorFormato.FechaAntesDeHoy => "fecha anterior a hoy",
        OperadorFormato.FechaHoy => "fecha es hoy",
        OperadorFormato.FechaDespuesDeHoy => "fecha posterior a hoy",
        OperadorFormato.FechaProximosDias => $"fecha en los próximos {r.Valor1} días",
        OperadorFormato.FechaUltimosDias => $"fecha en los últimos {r.Valor1} días",
        OperadorFormato.Formula => $"fórmula: {r.Valor1}",
        OperadorFormato.Duplicado => "valor repetido en la columna",
        OperadorFormato.Unico => "valor único en la columna",
        OperadorFormato.CodigoConEspacios => "código con espacios junto a los guiones",
        _ => ""
    };

    public static readonly IReadOnlyList<(OperadorFormato Op, string Nombre, int Valores)> Operadores = new[]
    {
        (OperadorFormato.Igual, "Es igual a", 1), (OperadorFormato.Diferente, "Es diferente de", 1),
        (OperadorFormato.Mayor, "Mayor que", 1), (OperadorFormato.MayorIgual, "Mayor o igual que", 1),
        (OperadorFormato.Menor, "Menor que", 1), (OperadorFormato.MenorIgual, "Menor o igual que", 1),
        (OperadorFormato.Entre, "Está entre", 2), (OperadorFormato.NoEntre, "No está entre", 2),
        (OperadorFormato.Contiene, "Contiene", 1), (OperadorFormato.NoContiene, "No contiene", 1),
        (OperadorFormato.EmpiezaCon, "Empieza con", 1), (OperadorFormato.TerminaCon, "Termina con", 1),
        (OperadorFormato.Vacio, "Está vacío", 0), (OperadorFormato.NoVacio, "No está vacío", 0),
        (OperadorFormato.FechaAntesDeHoy, "Fecha anterior a hoy", 0), (OperadorFormato.FechaHoy, "Fecha es hoy", 0),
        (OperadorFormato.FechaDespuesDeHoy, "Fecha posterior a hoy", 0),
        (OperadorFormato.FechaProximosDias, "Fecha en los próximos N días", 1), (OperadorFormato.FechaUltimosDias, "Fecha en los últimos N días", 1),
        (OperadorFormato.Duplicado, "Valores repetidos", 0), (OperadorFormato.Unico, "Valores únicos", 0),
        (OperadorFormato.CodigoConEspacios, "Código con espacios en los guiones", 0),
        (OperadorFormato.Formula, "Usar una fórmula", 1),
    };
}
