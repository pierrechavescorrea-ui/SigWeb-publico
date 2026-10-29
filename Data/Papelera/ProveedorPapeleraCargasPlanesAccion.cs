using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las cargas (Excel) de Planes de acción: eliminar la carga manda sus planes y sus
// acciones a la Papelera (misma FechaEliminacion); restaurarla los devuelve juntos.
public class ProveedorPapeleraCargasPlanesAccion : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCargasPlanesAccion(AppDbContext db) { _db = db; }

    public string Modulo => "Cargas de planes de acción";

    public List<RegistroPapelera> Listar()
    {
        var cargas = _db.CargasPlanesAccion.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = cargas.Select(c => c.Id).ToList();
        var n = _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().Where(p => p.CargaPlanesAccionId != null && ids.Contains(p.CargaPlanesAccionId.Value))
            .GroupBy(p => p.CargaPlanesAccionId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        return cargas.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id,
            Titulo = x.NombreArchivo + (string.IsNullOrWhiteSpace(x.Hoja) ? "" : " › " + x.Hoja),
            Subtitulo = $"{(n.TryGetValue(x.Id, out var k) ? k : 0)} plan(es) · cargado {x.Fecha:dd/MM/yyyy HH:mm}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var c = _db.CargasPlanesAccion.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            var planes = _db.PlanesAccion.IgnoreQueryFilters().Where(p => p.CargaPlanesAccionId == id && p.Eliminado && p.FechaEliminacion == c.FechaEliminacion).ToList();
            var ids = planes.Select(p => p.Id).ToList();
            foreach (var p in planes) { p.Eliminado = false; p.FechaEliminacion = null; }
            foreach (var a in _db.AccionesPlan.IgnoreQueryFilters().Where(a => ids.Contains(a.PlanAccionId) && a.Eliminado && a.FechaEliminacion == c.FechaEliminacion))
            { a.Eliminado = false; a.FechaEliminacion = null; }
            c.Eliminado = false; c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        if (_db.CargasPlanesAccion.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id) is null) return;
        var prov = new ProveedorPapeleraPlanesAccion(_db);
        foreach (var p in _db.PlanesAccion.IgnoreQueryFilters().Where(p => p.CargaPlanesAccionId == id && p.Eliminado).Select(p => p.Id).ToList())
            prov.EliminarDefinitivo(p);
        var c = _db.CargasPlanesAccion.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null) { _db.CargasPlanesAccion.Remove(c); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.CargasPlanesAccion.IgnoreQueryFilters()
                     .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
