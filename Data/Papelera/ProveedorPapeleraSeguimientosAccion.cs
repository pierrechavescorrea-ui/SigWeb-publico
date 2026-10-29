using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de un seguimiento de una acción de un plan (con sus soportes, que se borran con él).
public class ProveedorPapeleraSeguimientosAccion : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraSeguimientosAccion(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Seguimientos de acciones";

    public List<RegistroPapelera> Listar()
    {
        var l = _db.SeguimientosAccion.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = l.Select(x => x.AccionPlanId).Distinct().ToList();
        var acc = _db.AccionesPlan.IgnoreQueryFilters().AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionary(a => a.Id, a => a.Descripcion);
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id,
            Titulo = $"{x.Fecha:dd/MM/yyyy} · {EscalaAccion.Nombre(x.EstadoNuevo)}",
            Subtitulo = (acc.TryGetValue(x.AccionPlanId, out var d) ? d : "") + (string.IsNullOrWhiteSpace(x.Observacion) ? "" : " — " + x.Observacion),
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.SeguimientosAccion.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.SeguimientosAccion.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var a in _db.ArchivosAccionPlan.IgnoreQueryFilters().Where(a => a.SeguimientoAccionId == id).ToList())
            {
                try
                {
                    var ruta = Path.Combine(_env.WebRootPath, a.RutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(ruta)) File.Delete(ruta);
                }
                catch { }
                _db.ArchivosAccionPlan.Remove(a);
            }
            _db.SeguimientosAccion.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.SeguimientosAccion.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
