using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las carpetas de adjuntos de los planes de acción. Una carpeta eliminada se lleva sus
// archivos con la misma marca de tiempo y se restauran juntos.
public class ProveedorPapeleraCarpetasPlan : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCarpetasPlan(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Carpetas de planes de acción";

    public List<RegistroPapelera> Listar()
    {
        var l = _db.CarpetasPlan.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = l.Select(x => x.PlanAccionId).Distinct().ToList();
        var codigos = _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Codigo);
        var idsC = l.Select(x => x.Id).ToList();
        var archivos = _db.ArchivosCarpetaPlan.IgnoreQueryFilters().AsNoTracking().Where(a => idsC.Contains(a.CarpetaPlanId)).ToList();
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = x.Nombre,
            Subtitulo = (codigos.TryGetValue(x.PlanAccionId, out var c) ? c : $"Plan #{x.PlanAccionId}")
                + $" · {archivos.Count(a => a.CarpetaPlanId == x.Id && a.Eliminado && a.FechaEliminacion == x.FechaEliminacion)} archivo(s)",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.CarpetasPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var a in _db.ArchivosCarpetaPlan.IgnoreQueryFilters().Where(a => a.CarpetaPlanId == id && a.Eliminado && a.FechaEliminacion == x.FechaEliminacion))
            { a.Eliminado = false; a.FechaEliminacion = null; }
            x.Eliminado = false; x.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.CarpetasPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var a in _db.ArchivosCarpetaPlan.IgnoreQueryFilters().Where(a => a.CarpetaPlanId == id).ToList())
                ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, a.RutaArchivo);
            _db.CarpetasPlan.Remove(x);   // sus archivos, en cascada
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.CarpetasPlan.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

// Papelera de los archivos sueltos de las carpetas de planes (los que se fueron con su carpeta
// aparecen y se restauran con ella).
public class ProveedorPapeleraArchivosCarpetaPlan : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraArchivosCarpetaPlan(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Adjuntos de planes de acción";

    private IQueryable<ArchivoCarpetaPlan> Sueltos() =>
        _db.ArchivosCarpetaPlan.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.Eliminado && _db.CarpetasPlan.Any(c => c.Id == a.CarpetaPlanId));

    public List<RegistroPapelera> Listar()
    {
        var l = Sueltos().OrderByDescending(x => x.FechaEliminacion).ToList();
        var idsC = l.Select(x => x.CarpetaPlanId).Distinct().ToList();
        var carpetas = _db.CarpetasPlan.AsNoTracking().Where(c => idsC.Contains(c.Id)).ToDictionary(c => c.Id);
        var idsP = carpetas.Values.Select(c => c.PlanAccionId).Distinct().ToList();
        var codigos = _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().Where(p => idsP.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Codigo);
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = x.NombreOriginal,
            Subtitulo = carpetas.TryGetValue(x.CarpetaPlanId, out var c)
                ? $"{(codigos.TryGetValue(c.PlanAccionId, out var cod) ? cod : $"Plan #{c.PlanAccionId}")} › {c.Nombre}" : "",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.ArchivosCarpetaPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ArchivosCarpetaPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, x.RutaArchivo);
            _db.ArchivosCarpetaPlan.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in Sueltos().Where(x => x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
