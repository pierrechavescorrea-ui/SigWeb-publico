using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de los planes de acción. Eliminar solo marca Eliminado; al
// restaurar el plan vuelven con él las acciones que se fueron en el mismo
// momento.
public class ProveedorPapeleraPlanesAccion : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraPlanesAccion(AppDbContext db) { _db = db; }

    public string Modulo => "Planes de acción";

    public List<RegistroPapelera> Listar() =>
        _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking()
            // Los que se fueron con su carga los muestra (y restaura) la papelera de cargas.
            .Where(x => x.Eliminado && (x.CargaPlanesAccionId == null || _db.CargasPlanesAccion.Any(c => c.Id == x.CargaPlanesAccionId)))
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = $"{x.Codigo} — {x.DescripcionHallazgo}",
                Subtitulo = $"{x.Fecha:dd/MM/yyyy} · {x.Proceso}",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var p = _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (p is null) return avisos;
        if (_db.PlanesAccion.AsNoTracking().Any(x => x.Codigo == p.Codigo))
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "grave",
                Mensaje = $"Ya existe otro plan con el código \"{p.Codigo}\".",
                Solucion = "Cambia el código del plan vigente antes de restaurar este.",
                Menus = new() { "Planes de acción" }
            });
        if (p.RiesgoId is int rid && !_db.Riesgos.AsNoTracking().Any(r => r.Id == rid))
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "atencion",
                Mensaje = "El riesgo de origen de este plan está en la Papelera.",
                Solucion = "El plan se restaura igual; restaura el riesgo para ver la trazabilidad desde la matriz.",
                Menus = new() { "Riesgos" }
            });
        return avisos;
    }

    public void Restaurar(int id)
    {
        var x = _db.PlanesAccion.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        var cuando = x.FechaEliminacion;
        x.Eliminado = false; x.FechaEliminacion = null;
        // Vuelven las acciones que se eliminaron junto con el plan (misma marca de tiempo).
        foreach (var a in _db.AccionesPlan.IgnoreQueryFilters().Where(a => a.PlanAccionId == id && a.Eliminado && a.FechaEliminacion == cuando))
        { a.Eliminado = false; a.FechaEliminacion = null; }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.PlanesAccion.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        _db.PlanesAccion.Remove(x);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var v = _db.PlanesAccion.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).ToList();
        if (v.Count == 0) return;
        _db.PlanesAccion.RemoveRange(v);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}

public class ProveedorPapeleraAccionesPlan : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraAccionesPlan(AppDbContext db) { _db = db; }

    public string Modulo => "Acciones de planes";

    public List<RegistroPapelera> Listar()
    {
        var planes = _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().ToDictionary(p => p.Id, p => p.Codigo);
        var planesEliminados = _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().Where(p => p.Eliminado).Select(p => p.Id).ToHashSet();
        // Las que se fueron con su plan se ven en la entrada del plan, no sueltas.
        return _db.AccionesPlan.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
            .Where(x => !planesEliminados.Contains(x.PlanAccionId))
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Descripcion,
                Subtitulo = (planes.TryGetValue(x.PlanAccionId, out var c) ? c : "") + " · " + x.Responsable,
                FechaEliminacion = x.FechaEliminacion
            }).ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var a = _db.AccionesPlan.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (a is null) return avisos;
        if (!_db.PlanesAccion.AsNoTracking().Any(p => p.Id == a.PlanAccionId))
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "grave",
                Mensaje = "El plan de acción de esta acción está en la Papelera.",
                Solucion = "Restaura primero el plan (módulo \"Planes de acción\").",
                Menus = new() { "Planes de acción" }
            });
        return avisos;
    }

    public void Restaurar(int id)
    {
        var x = _db.AccionesPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        x.Eliminado = false; x.FechaEliminacion = null;
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.AccionesPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        _db.AccionesPlan.Remove(x);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var v = _db.AccionesPlan.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).ToList();
        if (v.Count == 0) return;
        _db.AccionesPlan.RemoveRange(v);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}

// Anexos del producto de una acción.
public class ProveedorPapeleraAnexosAccion : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraAnexosAccion(AppDbContext db) { _db = db; }

    public string Modulo => "Anexos de acciones";

    public List<RegistroPapelera> Listar()
    {
        var acciones = _db.AccionesPlan.IgnoreQueryFilters().AsNoTracking().ToDictionary(a => a.Id, a => a.Descripcion);
        return _db.ArchivosAccionPlan.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = x.NombreOriginal,
                Subtitulo = acciones.TryGetValue(x.AccionPlanId, out var d) ? d : "",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.ArchivosAccionPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        x.Eliminado = false; x.FechaEliminacion = null;
        _db.SaveChanges(); _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ArchivosAccionPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        _db.ArchivosAccionPlan.Remove(x);
        _db.SaveChanges(); _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var v = _db.ArchivosAccionPlan.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).ToList();
        if (v.Count == 0) return;
        _db.ArchivosAccionPlan.RemoveRange(v);
        _db.SaveChanges(); _db.ChangeTracker.Clear();
    }
}

// Soportes adjuntos a un seguimiento de riesgo.
public class ProveedorPapeleraArchivosRiesgo : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraArchivosRiesgo(AppDbContext db) { _db = db; }

    public string Modulo => "Soportes de seguimientos";

    public List<RegistroPapelera> Listar()
    {
        var segs = _db.SeguimientosRiesgo.IgnoreQueryFilters().AsNoTracking().ToDictionary(s => s.Id, s => s.Fecha);
        return _db.ArchivosRiesgo.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = x.NombreOriginal,
                Subtitulo = x.SeguimientoRiesgoId is int s && segs.TryGetValue(s, out var f) ? $"Seguimiento del {f:dd/MM/yyyy}" : "",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.ArchivosRiesgo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        x.Eliminado = false; x.FechaEliminacion = null;
        _db.SaveChanges(); _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ArchivosRiesgo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        _db.ArchivosRiesgo.Remove(x);
        _db.SaveChanges(); _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var v = _db.ArchivosRiesgo.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).ToList();
        if (v.Count == 0) return;
        _db.ArchivosRiesgo.RemoveRange(v);
        _db.SaveChanges(); _db.ChangeTracker.Clear();
    }
}
