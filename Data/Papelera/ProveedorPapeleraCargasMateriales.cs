using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las cargas (Excel) de Materiales: eliminar la carga manda sus materiales a la Papelera
// (misma FechaEliminacion); restaurarla los devuelve; el borrado definitivo los borra con sus archivos.
public class ProveedorPapeleraCargasMateriales : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCargasMateriales(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Cargas de materiales";

    public List<RegistroPapelera> Listar()
    {
        var cargas = _db.CargasMateriales.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = cargas.Select(c => c.Id).ToList();
        var n = _db.Materiales.AsNoTracking().Where(m => m.CargaMaterialesId != null && ids.Contains(m.CargaMaterialesId.Value))
            .GroupBy(m => m.CargaMaterialesId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        return cargas.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id,
            Titulo = x.NombreArchivo + (string.IsNullOrWhiteSpace(x.Hoja) ? "" : " › " + x.Hoja),
            Subtitulo = $"{(n.TryGetValue(x.Id, out var k) ? k : 0)} material(es) · cargado {x.Fecha:dd/MM/yyyy HH:mm}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var c = _db.CargasMateriales.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            foreach (var m in _db.Materiales.Where(m => m.CargaMaterialesId == id && m.Eliminado && m.FechaEliminacion == c.FechaEliminacion))
            { m.Eliminado = false; m.FechaEliminacion = null; }
            c.Eliminado = false; c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        if (_db.CargasMateriales.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id) is null) return;
        var prov = new ProveedorPapeleraMateriales(_db, _env);
        foreach (var m in _db.Materiales.Where(m => m.CargaMaterialesId == id && m.Eliminado).Select(m => m.Id).ToList())
            prov.EliminarDefinitivo(m);
        var c = _db.CargasMateriales.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null) { _db.CargasMateriales.Remove(c); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.CargasMateriales.IgnoreQueryFilters()
                     .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
