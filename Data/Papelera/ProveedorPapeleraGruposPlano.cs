using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las disciplinas del listado maestro de planos.
public class ProveedorPapeleraGruposPlano : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraGruposPlano(AppDbContext db) { _db = db; }

    public string Modulo => "Grupos de planos";

    public List<RegistroPapelera> Listar()
    {
        return _db.GruposPlano.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Nombre,
                Subtitulo = "Disciplina del listado maestro",
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.GruposPlano.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            x.Eliminado = false;
            x.FechaEliminacion = null;

            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.GruposPlano.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            _db.GruposPlano.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.GruposPlano.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .ToList();
        if (vencidos.Count == 0) return;
        _db.GruposPlano.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
