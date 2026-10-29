using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera del módulo Avance de obra: informes diarios (con sus registros y fotos, misma marca de
// tiempo), registros y fotos sueltos, y actividades de obra.
public class ProveedorPapeleraInformesDiarios : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraInformesDiarios(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Informes diarios de obra";

    public List<RegistroPapelera> Listar()
    {
        var l = _db.InformesDiarios.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = l.Select(x => x.Id).ToList();
        var n = _db.RegistrosAvance.IgnoreQueryFilters().AsNoTracking().Where(r => ids.Contains(r.InformeDiarioId))
            .GroupBy(r => r.InformeDiarioId).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = $"Informe del {x.Fecha:dd/MM/yyyy}",
            Subtitulo = $"{(n.TryGetValue(x.Id, out var k) ? k : 0)} registro(s) de avance",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.InformesDiarios.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var r in _db.RegistrosAvance.IgnoreQueryFilters().Where(r => r.InformeDiarioId == id && r.Eliminado && r.FechaEliminacion == x.FechaEliminacion))
            { r.Eliminado = false; r.FechaEliminacion = null; }
            foreach (var f in _db.FotosAvance.IgnoreQueryFilters().Where(f => f.InformeDiarioId == id && f.Eliminado && f.FechaEliminacion == x.FechaEliminacion))
            { f.Eliminado = false; f.FechaEliminacion = null; }
            x.Eliminado = false; x.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.InformesDiarios.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var f in _db.FotosAvance.IgnoreQueryFilters().Where(f => f.InformeDiarioId == id).ToList())
                ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, f.Ruta);
            _db.InformesDiarios.Remove(x);   // notas, registros y fotos en cascada
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.InformesDiarios.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraRegistrosAvance : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraRegistrosAvance(AppDbContext db) { _db = db; }

    public string Modulo => "Registros de avance de obra";

    // Los que se fueron con su informe o con su actividad aparecen (y se restauran) con ellos.
    private IQueryable<RegistroAvance> Sueltos() =>
        _db.RegistrosAvance.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.Eliminado && _db.InformesDiarios.Any(i => i.Id == r.InformeDiarioId && !i.Eliminado)
                && !_db.ActividadesObra.Any(a => a.Id == r.ActividadObraId && a.Eliminado && a.FechaEliminacion == r.FechaEliminacion));

    public List<RegistroPapelera> Listar()
    {
        var l = Sueltos().OrderByDescending(x => x.FechaEliminacion).ToList();
        var torres = l.Select(x => x.ElementoTrazadoId).Distinct().ToList();
        var nom = _db.ElementosTrazado.IgnoreQueryFilters().AsNoTracking().Where(e => torres.Contains(e.Id))
            .ToDictionary(e => e.Id, e => string.IsNullOrWhiteSpace(e.Nombre) ? e.Codigo : e.Nombre!);
        var acts = _db.ActividadesObra.IgnoreQueryFilters().AsNoTracking().ToDictionary(a => a.Id, a => a.Nombre);
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id,
            Titulo = $"{(acts.TryGetValue(x.ActividadObraId, out var a) ? a : "Actividad")} · torre {(nom.TryGetValue(x.ElementoTrazadoId, out var t) ? t : "#" + x.ElementoTrazadoId)}",
            Subtitulo = $"{x.Fecha:dd/MM/yyyy} · {(x.NoAplica ? "N/A" : x.AvanceAcumulado.ToString("0.#") + " %")}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.RegistrosAvance.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            x.Eliminado = false; x.FechaEliminacion = null;
            // Si su actividad está en la Papelera, vuelve con él (si no, el registro no contaría).
            var a = _db.ActividadesObra.IgnoreQueryFilters().FirstOrDefault(e => e.Id == x.ActividadObraId && e.Eliminado);
            if (a is not null) { a.Eliminado = false; a.FechaEliminacion = null; }
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.RegistrosAvance.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var f in _db.FotosAvance.IgnoreQueryFilters().Where(f => f.RegistroAvanceId == id)) f.RegistroAvanceId = null;
            _db.RegistrosAvance.Remove(x);
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

public class ProveedorPapeleraFotosAvance : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraFotosAvance(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Fotos de avance de obra";

    private IQueryable<FotoAvance> Sueltos() =>
        _db.FotosAvance.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.Eliminado && _db.InformesDiarios.Any(i => i.Id == f.InformeDiarioId && !i.Eliminado));

    public List<RegistroPapelera> Listar()
    {
        var l = Sueltos().OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = l.Select(x => x.InformeDiarioId).Distinct().ToList();
        var fechas = _db.InformesDiarios.IgnoreQueryFilters().AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionary(i => i.Id, i => i.Fecha);
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = x.Nombre,
            Subtitulo = fechas.TryGetValue(x.InformeDiarioId, out var f) ? $"Informe del {f:dd/MM/yyyy}" : "",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.FotosAvance.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.FotosAvance.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, x.Ruta);
            _db.FotosAvance.Remove(x);
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

public class ProveedorPapeleraActividadesObra : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraActividadesObra(AppDbContext db) { _db = db; }

    public string Modulo => "Actividades de obra";

    public List<RegistroPapelera> Listar() =>
        _db.ActividadesObra.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = x.Nombre,
                Subtitulo = $"{x.Grupo} · peso {x.Peso:0.###}",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public void Restaurar(int id)
    {
        var x = _db.ActividadesObra.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var r in _db.RegistrosAvance.IgnoreQueryFilters().Where(r => r.ActividadObraId == id && r.Eliminado && r.FechaEliminacion == x.FechaEliminacion))
            { r.Eliminado = false; r.FechaEliminacion = null; }
            x.Eliminado = false; x.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    // Sus registros se van con ella (la relación es Restrict: se borran primero).
    public void EliminarDefinitivo(int id)
    {
        var x = _db.ActividadesObra.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            var regs = _db.RegistrosAvance.IgnoreQueryFilters().Where(r => r.ActividadObraId == id).ToList();
            var ids = regs.Select(r => r.Id).ToList();
            foreach (var f in _db.FotosAvance.IgnoreQueryFilters().Where(f => f.RegistroAvanceId != null && ids.Contains(f.RegistroAvanceId.Value))) f.RegistroAvanceId = null;
            _db.RegistrosAvance.RemoveRange(regs);
            _db.ActividadesObra.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.ActividadesObra.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
