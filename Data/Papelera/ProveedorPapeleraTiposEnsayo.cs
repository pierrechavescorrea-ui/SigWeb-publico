using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de los tipos de ensayo. Al eliminar uno se desactiva su actividad en Trazado; al restaurar se reactiva.
public class ProveedorPapeleraTiposEnsayo : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraTiposEnsayo(AppDbContext db) { _db = db; }

    public string Modulo => "Tipos de ensayo";

    public List<RegistroPapelera> Listar()
    {
        return _db.TiposEnsayo.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Nombre,
                Subtitulo = "Tipo de ensayo",
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var t = _db.TiposEnsayo.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(e => e.Id == id);
        if (t is null) return avisos;
        var g = _db.GruposEnsayo.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(e => e.Id == t.GrupoEnsayoId);
        if (g is null || g.Eliminado)
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "grave",
                Mensaje = $"El grupo de ensayos \"{g?.Nombre ?? "?"}\" al que pertenece este tipo está en la Papelera: el tipo volvería sin grupo visible.",
                Solucion = "Restaura primero el grupo en la Papelera (módulo \"Grupos de ensayos\").",
                Menus = new() { "Ensayos", "Matriz PIE" }
            });
        return avisos;
    }

    public void Restaurar(int id)
    {
        var x = _db.TiposEnsayo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            x.Eliminado = false;
            x.FechaEliminacion = null;

            foreach (var a in _db.Actividades.IgnoreQueryFilters().Where(a => a.TipoEnsayoId == id)) a.Activo = true;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.TiposEnsayo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            _db.TiposEnsayo.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.TiposEnsayo.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .ToList();
        if (vencidos.Count == 0) return;
        _db.TiposEnsayo.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
