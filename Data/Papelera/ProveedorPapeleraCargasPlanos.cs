using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las cargas (Excel) del listado maestro de planos: eliminar la carga manda a la Papelera
// los planos que creó (misma FechaEliminacion); restaurarla los devuelve; el borrado definitivo los
// borra con sus archivos (igual que el borrado definitivo de un plano).
public class ProveedorPapeleraCargasPlanos : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCargasPlanos(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Cargas del listado de planos";

    public List<RegistroPapelera> Listar()
    {
        var cargas = _db.CargasPlanos.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = cargas.Select(c => c.Id).ToList();
        var n = _db.PlanosMaestro.AsNoTracking().Where(p => p.CargaPlanosId != null && ids.Contains(p.CargaPlanosId.Value))
            .GroupBy(p => p.CargaPlanosId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        return cargas.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id,
            Titulo = x.NombreArchivo + (string.IsNullOrWhiteSpace(x.Hoja) ? "" : " › " + x.Hoja),
            Subtitulo = $"{(n.TryGetValue(x.Id, out var k) ? k : 0)} plano(s) · cargado {x.Fecha:dd/MM/yyyy HH:mm}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var c = _db.CargasPlanos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            foreach (var p in _db.PlanosMaestro.Where(p => p.CargaPlanosId == id && p.Eliminado && p.FechaEliminacion == c.FechaEliminacion))
            { p.Eliminado = false; p.FechaEliminacion = null; }
            c.Eliminado = false; c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.CargasPlanos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is null) return;
        var planos = _db.PlanosMaestro.Where(p => p.CargaPlanosId == id).Select(p => p.Id).ToList();
        var prov = new ProveedorPapeleraPlanoMaestro(_db, _env);
        foreach (var p in planos) prov.EliminarDefinitivo(p);
        var cc = _db.CargasPlanos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (cc is not null) { _db.CargasPlanos.Remove(cc); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.CargasPlanos.IgnoreQueryFilters()
                     .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
