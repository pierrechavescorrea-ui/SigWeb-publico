using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de los catálogos del módulo Catálogos.
public class ProveedorPapeleraDefinicionesCatalogo : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraDefinicionesCatalogo(AppDbContext db) { _db = db; }

    public string Modulo => "Catálogos";

    public List<RegistroPapelera> Listar()
    {
        return _db.DefinicionesCatalogo.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Nombre,
                Subtitulo = string.IsNullOrWhiteSpace(x.EntidadAsociada) ? "Catálogo" : x.EntidadAsociada,
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.DefinicionesCatalogo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
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
        var x = _db.DefinicionesCatalogo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            _db.DefinicionesCatalogo.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.DefinicionesCatalogo.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .ToList();
        if (vencidos.Count == 0) return;
        _db.DefinicionesCatalogo.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
