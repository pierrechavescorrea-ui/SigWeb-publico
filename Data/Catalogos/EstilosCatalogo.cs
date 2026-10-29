using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Catalogos;

// Formato (color, fondo, forma) que cada ítem de catálogo define en Catálogos,
// para mostrar sus valores igual en todas las listas. Se carga una vez con su
// propio DbContext (no choca con el de las páginas) y se recarga al guardar
// un ítem en Catálogos.
public class EstilosCatalogo
{
    private readonly IServiceScopeFactory _scopes;
    private readonly object _l = new();
    private Dictionary<string, EstiloCelda>? _cache;

    public EstilosCatalogo(IServiceScopeFactory scopes) { _scopes = scopes; }

    private static string Clave(string tipo, string valor) => tipo.Trim().ToLowerInvariant() + "\u001f" + valor.Trim().ToLowerInvariant();

    public EstiloCelda? Estilo(string tipo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var c = Cache();
        return c.TryGetValue(Clave(tipo, valor), out var e) ? e : null;
    }

    public void Recargar() { lock (_l) _cache = null; }

    private Dictionary<string, EstiloCelda> Cache()
    {
        lock (_l)
        {
            if (_cache is not null) return _cache;
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var d = new Dictionary<string, EstiloCelda>();
            foreach (var i in db.Catalogos.AsNoTracking().Where(i => i.Estilo != null && i.Estilo != "").ToList())
                d[Clave(i.Tipo, i.Nombre)] = De(i)!;
            return _cache = d;
        }
    }

    public static EstiloCelda? De(ItemCatalogo i)
    {
        if (string.IsNullOrWhiteSpace(i.Estilo)) return null;
        return MotorTabla.EstiloDe(new FormatoCondicional
        {
            Estilo = i.Estilo!, ColorTexto = i.ColorTexto, ColorFondo = i.ColorFondo,
            Forma = string.IsNullOrWhiteSpace(i.Forma) ? "pildora" : i.Forma, Negrita = i.Negrita
        });
    }
}
