using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de la matriz de riesgos. Tres proveedores, uno por nivel:
// la revisión completa, el riesgo y el seguimiento periódico. En los tres
// casos "eliminar" solo marca Eliminado, así que los hallazgos ya
// registrados en las hojas de vida de las torres se recuperan íntegros al
// restaurar.
public class ProveedorPapeleraVersionesMatrizRiesgos : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraVersionesMatrizRiesgos(AppDbContext db) { _db = db; }

    public string Modulo => "Matriz de riesgos (revisiones)";

    public List<RegistroPapelera> Listar() =>
        _db.VersionesMatrizRiesgos.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Codigo,
                Subtitulo = $"{x.Fecha:dd/MM/yyyy} · {x.ElaboradoPor}",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var v = _db.VersionesMatrizRiesgos.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (v is null) return avisos;
        if (v.Estado == EstadoVersionMatriz.Vigente && _db.VersionesMatrizRiesgos.AsNoTracking().Any(x => x.Estado == EstadoVersionMatriz.Vigente))
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "atencion",
                Mensaje = "Esta revisión estaba marcada como vigente y ya hay otra vigente.",
                Solucion = "Al restaurarla queda como histórica; márcala vigente a mano si corresponde.",
                Menus = new() { "Riesgos" }
            });
        return avisos;
    }

    public void Restaurar(int id)
    {
        var x = _db.VersionesMatrizRiesgos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        x.Eliminado = false; x.FechaEliminacion = null;
        // Nunca vuelve directo a "vigente": evita dos vigentes a la vez.
        if (x.Estado == EstadoVersionMatriz.Vigente && _db.VersionesMatrizRiesgos.Any(v => !v.Eliminado && v.Id != id && v.Estado == EstadoVersionMatriz.Vigente))
            x.Estado = EstadoVersionMatriz.Historica;
        foreach (var r in _db.Riesgos.IgnoreQueryFilters().Where(r => r.VersionMatrizRiesgosId == id && r.Eliminado))
        { r.Eliminado = false; r.FechaEliminacion = null; }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.VersionesMatrizRiesgos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        SoltarReferencias(_db, new[] { id });
        _db.VersionesMatrizRiesgos.Remove(x);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    // Las revisiones y riesgos posteriores apuntan a los anteriores (VersionAnteriorId, RiesgoAnteriorId,
    // sin cascada para no arrastrar la revisión nueva). Antes de borrar definitivamente, esas referencias
    // se sueltan (quedan en null); si no, la base rechaza el borrado.
    public static void SoltarReferencias(AppDbContext db, IReadOnlyCollection<int> idsVersiones)
    {
        db.VersionesMatrizRiesgos.IgnoreQueryFilters()
            .Where(v => v.VersionAnteriorId != null && idsVersiones.Contains(v.VersionAnteriorId.Value))
            .ExecuteUpdate(s => s.SetProperty(v => v.VersionAnteriorId, (int?)null));
        var idsRiesgos = db.Riesgos.IgnoreQueryFilters().Where(r => idsVersiones.Contains(r.VersionMatrizRiesgosId)).Select(r => r.Id).ToList();
        SoltarRiesgos(db, idsRiesgos);
    }

    public static void SoltarRiesgos(AppDbContext db, IReadOnlyCollection<int> idsRiesgos)
    {
        if (idsRiesgos.Count == 0) return;
        db.Riesgos.IgnoreQueryFilters()
            .Where(r => r.RiesgoAnteriorId != null && idsRiesgos.Contains(r.RiesgoAnteriorId.Value))
            .ExecuteUpdate(s => s.SetProperty(r => r.RiesgoAnteriorId, (int?)null));
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var v = _db.VersionesMatrizRiesgos.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).ToList();
        if (v.Count == 0) return;
        SoltarReferencias(_db, v.Select(x => x.Id).ToList());
        _db.VersionesMatrizRiesgos.RemoveRange(v);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}

public class ProveedorPapeleraRiesgos : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraRiesgos(AppDbContext db) { _db = db; }

    public string Modulo => "Riesgos";

    public List<RegistroPapelera> Listar()
    {
        var versiones = _db.VersionesMatrizRiesgos.IgnoreQueryFilters().AsNoTracking().ToDictionary(v => v.Id, v => v.Codigo);
        return _db.Riesgos.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = $"{x.Codigo} — {x.Descripcion}",
                Subtitulo = versiones.TryGetValue(x.VersionMatrizRiesgosId, out var c) ? c : "",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var r = _db.Riesgos.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (r is null) return avisos;
        var v = _db.VersionesMatrizRiesgos.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == r.VersionMatrizRiesgosId);
        if (v is null || v.Eliminado)
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "grave",
                Mensaje = "La revisión de la matriz a la que pertenece este riesgo está en la Papelera.",
                Solucion = "Restaura primero la revisión (módulo \"Matriz de riesgos (revisiones)\").",
                Menus = new() { "Riesgos" }
            });
        if (_db.Riesgos.AsNoTracking().Any(x => x.VersionMatrizRiesgosId == r.VersionMatrizRiesgosId && x.Codigo == r.Codigo))
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "atencion",
                Mensaje = $"Ya existe otro riesgo con el código \"{r.Codigo}\" en esa revisión.",
                Solucion = "Restaura y luego renumera uno de los dos desde Riesgos.",
                Menus = new() { "Riesgos" }
            });
        return avisos;
    }

    public void Restaurar(int id)
    {
        var x = _db.Riesgos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        x.Eliminado = false; x.FechaEliminacion = null;
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.Riesgos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        ProveedorPapeleraVersionesMatrizRiesgos.SoltarRiesgos(_db, new[] { id });
        _db.Riesgos.Remove(x);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var v = _db.Riesgos.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).ToList();
        if (v.Count == 0) return;
        ProveedorPapeleraVersionesMatrizRiesgos.SoltarRiesgos(_db, v.Select(x => x.Id).ToList());
        _db.Riesgos.RemoveRange(v);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}

public class ProveedorPapeleraSeguimientosRiesgo : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraSeguimientosRiesgo(AppDbContext db) { _db = db; }

    public string Modulo => "Seguimientos de riesgo";

    public List<RegistroPapelera> Listar()
    {
        var riesgos = _db.Riesgos.IgnoreQueryFilters().AsNoTracking().ToDictionary(r => r.Id, r => r.Codigo + " — " + r.Descripcion);
        return _db.SeguimientosRiesgo.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = $"Seguimiento del {x.Fecha:dd/MM/yyyy}",
                Subtitulo = riesgos.TryGetValue(x.RiesgoId, out var n) ? n : "",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.SeguimientosRiesgo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        x.Eliminado = false; x.FechaEliminacion = null;
        foreach (var h in _db.HallazgosRiesgo.IgnoreQueryFilters().Where(h => h.SeguimientoRiesgoId == id && h.Eliminado))
        { h.Eliminado = false; h.FechaEliminacion = null; }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.SeguimientosRiesgo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        _db.SeguimientosRiesgo.Remove(x);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var v = _db.SeguimientosRiesgo.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).ToList();
        if (v.Count == 0) return;
        _db.SeguimientosRiesgo.RemoveRange(v);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
