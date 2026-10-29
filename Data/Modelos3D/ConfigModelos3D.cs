using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SigWeb.Data.Modelos3D;

// Configuración editable de los modelos 3D: nombre principal (el que se relaciona con el tipo de la tabla de
// torres), descripción/observaciones y cuál modelo de un mismo nombre es el vinculado a la tabla de torres.
// Se guarda en wwwroot/uploads/modelos3d-config.json (junto a los demás archivos cargados por el usuario).
public class EntradaManifiesto
{
    [JsonPropertyName("codigo")] public string Codigo { get; set; } = "";
    [JsonPropertyName("archivo")] public string Archivo { get; set; } = "";
    [JsonPropertyName("familia")] public string? Familia { get; set; }
    [JsonPropertyName("nombre")] public string? Nombre { get; set; }
    [JsonPropertyName("vinculado")] public bool? Vinculado { get; set; }
}

public class ConfigModelo
{
    public string Codigo { get; set; } = "";
    public string Archivo { get; set; } = "";
    public string? Familia { get; set; }
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool Vinculado { get; set; } = true;
}

public static class ConfigModelos3D
{
    private static string RutaConfig(string webRoot) => Path.Combine(webRoot, "uploads", "modelos3d-config.json");
    private static readonly JsonSerializerOptions Opc = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static List<EntradaManifiesto> LeerManifiesto(string webRoot)
    {
        try
        {
            var ruta = Path.Combine(webRoot, "modelos3d", "modelos.json");
            return JsonSerializer.Deserialize<List<EntradaManifiesto>>(File.ReadAllText(ruta)) ?? new();
        }
        catch { return new(); }
    }

    // "BP/6", "bp 6", "BP-6" -> "BP6"
    public static string Norm(string? t) => Regex.Replace((t ?? "").ToUpperInvariant(), @"[^A-Z0-9]", "");

    // Manifiesto + lo guardado por el usuario. Dentro de un mismo nombre solo un modelo queda vinculado.
    public static List<ConfigModelo> Cargar(string webRoot)
    {
        var guardado = new Dictionary<string, ConfigModelo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var ruta = RutaConfig(webRoot);
            if (File.Exists(ruta))
                foreach (var c in JsonSerializer.Deserialize<List<ConfigModelo>>(File.ReadAllText(ruta), Opc) ?? new()) guardado[c.Codigo] = c;
        }
        catch { /* se usan los valores del manifiesto */ }

        var lista = new List<ConfigModelo>();
        foreach (var m in LeerManifiesto(webRoot))
        {
            guardado.TryGetValue(m.Codigo, out var g);
            lista.Add(new ConfigModelo
            {
                Codigo = m.Codigo, Archivo = m.Archivo, Familia = m.Familia,
                Nombre = !string.IsNullOrWhiteSpace(g?.Nombre) ? g!.Nombre : (m.Nombre ?? m.Codigo),
                Descripcion = g?.Descripcion ?? "",
                Vinculado = g?.Vinculado ?? m.Vinculado ?? true,
            });
        }
        Normalizar(lista);
        return lista;
    }

    // En cada grupo de modelos con el mismo nombre queda como máximo uno vinculado (el primero marcado).
    public static void Normalizar(List<ConfigModelo> lista)
    {
        foreach (var g in lista.GroupBy(c => Norm(c.Nombre)))
        {
            var vinc = g.Where(c => c.Vinculado).ToList();
            var elegido = vinc.FirstOrDefault();   // como máximo uno por nombre (puede quedar ninguno)
            foreach (var c in g) c.Vinculado = ReferenceEquals(c, elegido);
        }
    }

    public static void Guardar(string webRoot, List<ConfigModelo> lista)
    {
        var ruta = RutaConfig(webRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, JsonSerializer.Serialize(lista.Select(c => new { c.Codigo, c.Nombre, c.Descripcion, c.Vinculado }), Opc));
    }

    // Modelo vinculado a la tabla de torres para un tipo (texto de la tabla, o familia + cuerpo). Null = sin modelo.
    public static ConfigModelo? ModeloPara(List<ConfigModelo> cfg, string? tipoTexto, string? familia, int cuerpo)
    {
        var vinc = cfg.Where(c => c.Vinculado).ToList();
        var k1 = Norm(tipoTexto);
        if (k1.Length > 0 && vinc.FirstOrDefault(c => Norm(c.Nombre) == k1) is { } a) return a;
        var k2 = Norm(familia) + (cuerpo > 0 ? cuerpo.ToString() : "");
        if (k2.Length > 0 && vinc.FirstOrDefault(c => Norm(c.Nombre) == k2) is { } b) return b;
        return null;
    }
}
