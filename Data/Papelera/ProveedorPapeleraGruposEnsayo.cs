using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de los grupos de ensayos (Concretos, Suelos...).
public class ProveedorPapeleraGruposEnsayo : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraGruposEnsayo(AppDbContext db) { _db = db; }

    public string Modulo => "Grupos de ensayos";

    public List<RegistroPapelera> Listar()
    {
        return _db.GruposEnsayo.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Nombre,
                Subtitulo = "Grupo de ensayos",
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.GruposEnsayo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            x.Eliminado = false;
            var fecha = x.FechaEliminacion;
            x.FechaEliminacion = null;
            // Restaura también los tipos que se eliminaron junto con el grupo (misma marca de tiempo).
            if (fecha is not null)
            {
                var tipos = _db.TiposEnsayo.IgnoreQueryFilters()
                    .Where(t => t.GrupoEnsayoId == id && t.Eliminado && t.FechaEliminacion == fecha).ToList();
                foreach (var t in tipos)
                {
                    t.Eliminado = false; t.FechaEliminacion = null;
                    foreach (var a in _db.Actividades.IgnoreQueryFilters().Where(a => a.TipoEnsayoId == t.Id)) a.Activo = true;
                }
            }

            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.GruposEnsayo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            _db.GruposEnsayo.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.GruposEnsayo.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .ToList();
        if (vencidos.Count == 0) return;
        _db.GruposEnsayo.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
