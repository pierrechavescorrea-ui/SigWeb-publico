using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Iconos;

// Caché (singleton) de los íconos asignados desde Catálogos a puntos de la app.
// Ico, NavMenu, EncabezadoPagina y los mosaicos de ámbito la consultan al dibujarse.
public class IconosApp
{
    private readonly IServiceScopeFactory _scopes;
    private Dictionary<string, string>? _rutas;
    private Dictionary<int, string>? _porId;
    private readonly object _lock = new();

    public IconosApp(IServiceScopeFactory scopes) { _scopes = scopes; }

    public event Action? Cambio;

    private Dictionary<string, string> Rutas
    {
        get
        {
            if (_rutas is not null) return _rutas;
            lock (_lock)
            {
                if (_rutas is not null) return _rutas;
                try
                {
                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    _rutas = (from a in db.AsignacionesIcono.AsNoTracking()
                              join i in db.IconosCatalogo.AsNoTracking() on a.IconoCatalogoId equals i.Id
                              where i.Activo
                              select new { a.Punto, i.RutaArchivo }).ToList()
                             .GroupBy(x => x.Punto).ToDictionary(g => g.Key, g => g.First().RutaArchivo, StringComparer.OrdinalIgnoreCase);
                }
                catch { _rutas = new(StringComparer.OrdinalIgnoreCase); }   // p. ej. antes de aplicar la migración
                return _rutas;
            }
        }
    }

    // Ruta de un ícono de la galería por su Id (para elementos con IconoCatalogoId propio).
    public string? RutaIcono(int? id)
    {
        if (id is null) return null;
        var mapa = _porId;
        if (mapa is null)
        {
            lock (_lock)
            {
                if (_porId is null)
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        _porId = db.IconosCatalogo.AsNoTracking().Where(i => i.Activo).Select(i => new { i.Id, i.RutaArchivo }).ToList()
                                   .ToDictionary(x => x.Id, x => x.RutaArchivo);
                    }
                    catch { _porId = new(); }
                }
                mapa = _porId;
            }
        }
        return mapa.TryGetValue(id.Value, out var r) ? r : null;
    }

    // Ruta del ícono asignado al punto, o null si usa el de por defecto.
    public string? Ruta(string punto) => Rutas.TryGetValue(punto, out var r) ? r : null;

    public bool Asignado(string punto) => Rutas.ContainsKey(punto);

    public async Task AsignarAsync(AppDbContext db, string punto, int iconoId)
    {
        var a = await db.AsignacionesIcono.FirstOrDefaultAsync(x => x.Punto == punto);
        if (a is null) { a = new AsignacionIcono { Punto = punto }; db.AsignacionesIcono.Add(a); }
        a.IconoCatalogoId = iconoId; a.Fecha = DateTime.Now;
        await db.SaveChangesAsync();
        Recargar();
    }

    public async Task RestablecerAsync(AppDbContext db, string punto)
    {
        var a = await db.AsignacionesIcono.FirstOrDefaultAsync(x => x.Punto == punto);
        if (a is not null) { db.AsignacionesIcono.Remove(a); await db.SaveChangesAsync(); }
        Recargar();
    }

    // Puntos donde se usa un ícono (para mostrarlos en su tarjeta).
    public List<string> PuntosDe(string ruta) => Rutas.Where(kv => kv.Value == ruta).Select(kv => kv.Key).ToList();

    public void Recargar()
    {
        lock (_lock) { _rutas = null; _porId = null; }
        Cambio?.Invoke();
    }
}
