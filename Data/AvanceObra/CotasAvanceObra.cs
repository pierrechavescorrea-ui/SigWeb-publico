using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SigWeb.Data.AvanceObra;

// Qué columna de la tabla de torres (CampoExtraTorre / CampoExtraComponente) da cada cota del visor 3D.
// Se guarda como JSON en ConfiguracionEmpresa.AvanceObraCotas (Avance de obra › Ajustes).
// Null o vacío = no hay columna: el visor usa la cota genérica y la marca como supuesto.
public class ConfigCotasAvance
{
    public string? ClaveTipoTorre { get; set; }        // null = la misma regla de Línea 3D ("cuerpo", si no "tipo")
    public string? ClaveTipoCimentacion { get; set; }  // valor con "parrilla" = pata con parrilla
    public string? ExcLargo { get; set; }
    public string? ExcAncho { get; set; }
    public string? ExcProf { get; set; }
    public string? ZapLado { get; set; }
    public string? ZapAlto { get; set; }
    public string? PedLado { get; set; }
    public string? PedAlto { get; set; }

    public static ConfigCotasAvance Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<ConfigCotasAvance>(json) ?? new(); }
        catch { return new(); }
    }

    public string Escribir() => JsonSerializer.Serialize(this);

    // Nombre visible de cada cota y su valor genérico (m).
    public static readonly (string Campo, string Nombre, double Generico)[] Cotas =
    {
        (nameof(ExcLargo), "Excavación: largo", 3.0),
        (nameof(ExcAncho), "Excavación: ancho", 3.0),
        (nameof(ExcProf), "Excavación: profundidad", 3.2),
        (nameof(ZapLado), "Zapata: lado", 2.4),
        (nameof(ZapAlto), "Zapata: alto", 0.45),
        (nameof(PedLado), "Pedestal: lado", 0.7),
        (nameof(PedAlto), "Pedestal: alto sobre zapata", 3.0),
    };

    public string? Clave(string campo) => campo switch
    {
        nameof(ExcLargo) => ExcLargo, nameof(ExcAncho) => ExcAncho, nameof(ExcProf) => ExcProf,
        nameof(ZapLado) => ZapLado, nameof(ZapAlto) => ZapAlto, nameof(PedLado) => PedLado, nameof(PedAlto) => PedAlto,
        _ => null
    };

    public void Asignar(string campo, string? clave)
    {
        clave = string.IsNullOrWhiteSpace(clave) ? null : clave;
        switch (campo)
        {
            case nameof(ExcLargo): ExcLargo = clave; break;
            case nameof(ExcAncho): ExcAncho = clave; break;
            case nameof(ExcProf): ExcProf = clave; break;
            case nameof(ZapLado): ZapLado = clave; break;
            case nameof(ZapAlto): ZapAlto = clave; break;
            case nameof(PedLado): PedLado = clave; break;
            case nameof(PedAlto): PedAlto = clave; break;
        }
    }
}

// Cotas de una pata ya resueltas (m). Generico = true si no salió de la tabla de torres.
public class CotasPata
{
    public double ExcLargo { get; set; }
    public double ExcAncho { get; set; }
    public double ExcProf { get; set; }
    public double ZapLado { get; set; }
    public double ZapAlto { get; set; }
    public double PedLado { get; set; }
    public double PedAlto { get; set; }
    public bool Parrilla { get; set; }             // parrilla liviana o pesada: acero y concreto no aplican
    public bool Pesada { get; set; }               // parrilla pesada (más vigas y montantes)
    public bool Pila { get; set; }                 // pila excavada: fuste cilíndrico con anillos de 0,5 m
    public string Cimentacion { get; set; } = "";  // tipo leído de la tabla (pata o torre); "" = sin dato
    public bool CimentacionSinDetalle { get; set; } // "mixta" en la torre sin tipo por pata: se dibuja zapata
    public List<string> Genericas { get; set; } = new();   // nombres de las cotas que no salieron de la tabla
}

public static class CotasAvanceObra
{
    // "3,20" / "3.20 m" / "3200 mm" → 3.2. SUPUESTO: un número mayor que 50 sin unidad viene en milímetros.
    public static double? Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var m = Regex.Match(texto, @"-?\d+(?:[.,]\d+)?");
        if (!m.Success) return null;
        if (!double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v <= 0) return null;
        var t = texto.ToLowerInvariant();
        if (t.Contains("mm") || (!t.Contains(" m") && !t.EndsWith("m") && v > 50)) v /= 1000.0;
        else if (t.Contains("cm")) v /= 100.0;
        return v;
    }

    // Valor de la pata (CampoExtraComponente) y, si no tiene, el de la torre (CampoExtraTorre).
    // Columna del tipo de cimentación: la configurada; si no, la que diga "cimentación" o "fundación" (mejor con "tipo").
    public static string? ClaveCimentacion(ConfigCotasAvance cfg, IEnumerable<string> claves)
    {
        if (!string.IsNullOrWhiteSpace(cfg.ClaveTipoCimentacion)) return cfg.ClaveTipoCimentacion;
        var l = claves.Where(c => (c.Contains("cimenta", StringComparison.OrdinalIgnoreCase) || c.Contains("fundaci", StringComparison.OrdinalIgnoreCase)
            || c.Contains("fundacion", StringComparison.OrdinalIgnoreCase))
            && !c.Contains("plano", StringComparison.OrdinalIgnoreCase) && !c.Contains("prof", StringComparison.OrdinalIgnoreCase)
            && !c.Contains("nivel", StringComparison.OrdinalIgnoreCase)).ToList();
        return l.FirstOrDefault(c => c.Contains("tipo", StringComparison.OrdinalIgnoreCase)) ?? l.FirstOrDefault();
    }

    public static CotasPata Resolver(ConfigCotasAvance cfg, IReadOnlyDictionary<string, string> torre, IReadOnlyDictionary<string, string>? pata, string? claveCim = null)
    {
        string? Texto(string? clave)
        {
            if (string.IsNullOrWhiteSpace(clave)) return null;
            if (pata is not null && pata.TryGetValue(clave, out var vp) && !string.IsNullOrWhiteSpace(vp)) return vp;
            return torre.TryGetValue(clave, out var vt) ? vt : null;
        }
        var r = new CotasPata();
        foreach (var (campo, nombre, gen) in ConfigCotasAvance.Cotas)
        {
            var v = Numero(Texto(cfg.Clave(campo)));
            if (v is null) r.Genericas.Add(nombre);
            var x = v ?? gen;
            switch (campo)
            {
                case nameof(ConfigCotasAvance.ExcLargo): r.ExcLargo = x; break;
                case nameof(ConfigCotasAvance.ExcAncho): r.ExcAncho = x; break;
                case nameof(ConfigCotasAvance.ExcProf): r.ExcProf = x; break;
                case nameof(ConfigCotasAvance.ZapLado): r.ZapLado = x; break;
                case nameof(ConfigCotasAvance.ZapAlto): r.ZapAlto = x; break;
                case nameof(ConfigCotasAvance.PedLado): r.PedLado = x; break;
                case nameof(ConfigCotasAvance.PedAlto): r.PedAlto = x; break;
            }
        }
        // Tipo de cimentación: el de la pata manda; "mixta" en la torre solo dice que las patas difieren.
        var cim = (Texto(claveCim ?? cfg.ClaveTipoCimentacion) ?? "").Trim();
        var n = cim.ToLowerInvariant();
        r.Cimentacion = cim;
        r.Parrilla = n.Contains("parrilla");
        r.Pesada = r.Parrilla && n.Contains("pesad");
        r.Pila = !r.Parrilla && n.Contains("pila");
        r.CimentacionSinDetalle = !r.Parrilla && !r.Pila && n.Contains("mixt");
        if (r.Pila)
        {
            // SUPUESTO (pedido de obra): sin cotas en la tabla, pila de 6 m de profundidad y 1,5 m de diámetro.
            if (r.Genericas.Contains("Excavación: profundidad")) r.ExcProf = 6.0;
            if (r.Genericas.Contains("Excavación: largo")) r.ExcLargo = 1.5;
            if (r.Genericas.Contains("Excavación: ancho")) r.ExcAncho = r.ExcLargo;
        }
        // Que la zapata quepa en la excavación (dato incoherente de la tabla → se recorta para dibujar).
        r.ZapLado = Math.Min(r.ZapLado, Math.Min(r.ExcLargo, r.ExcAncho) * 0.95);
        r.PedLado = Math.Min(r.PedLado, r.ZapLado);
        return r;
    }
}
