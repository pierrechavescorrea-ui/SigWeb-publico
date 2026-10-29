using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera del listado de comunicados: comunicados (con sus adjuntos, misma marca de tiempo),
// adjuntos sueltos (archivos y enlaces a la nube) y cargas de Excel (con sus comunicados).
public class ProveedorPapeleraComunicados : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraComunicados(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Comunicados";

    // Los que se fueron con su carga aparecen (y se restauran) con ella.
    private IQueryable<Comunicado> Sueltos() =>
        _db.Comunicados.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Eliminado && (c.CargaComunicadosId == null || _db.CargasComunicados.Any(g => g.Id == c.CargaComunicadosId)));

    public List<RegistroPapelera> Listar() =>
        Sueltos().OrderByDescending(x => x.FechaEliminacion).ToList().Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = x.Consecutivo,
            Subtitulo = $"{x.Fecha:dd/MM/yyyy} · {x.Descripcion}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();

    public void Restaurar(int id)
    {
        var c = _db.Comunicados.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            foreach (var a in _db.AdjuntosComunicado.IgnoreQueryFilters().Where(a => a.ComunicadoId == id && a.Eliminado && a.FechaEliminacion == c.FechaEliminacion))
            { a.Eliminado = false; a.FechaEliminacion = null; }
            c.Eliminado = false; c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.Comunicados.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            foreach (var a in _db.AdjuntosComunicado.IgnoreQueryFilters().Where(a => a.ComunicadoId == id && a.Clase == AdjuntoComunicado.Archivo).ToList())
                ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, a.Ruta);
            // Sus valores de Campos (tabla "comunicados").
            var campos = _db.CamposTabla.IgnoreQueryFilters().Where(x => x.Tabla == SigWeb.Data.Comunicados.LogicaComunicados.Tabla).Select(x => x.Id).ToList();
            _db.ValoresCampoTabla.RemoveRange(_db.ValoresCampoTabla.Where(v => campos.Contains(v.CampoTablaId) && v.FilaId == id));
            _db.Comunicados.Remove(c);   // adjuntos en cascada
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

public class ProveedorPapeleraAdjuntosComunicado : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraAdjuntosComunicado(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Adjuntos de comunicados";

    // Los que se fueron con su comunicado aparecen con él.
    private IQueryable<AdjuntoComunicado> Sueltos() =>
        _db.AdjuntosComunicado.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.Eliminado && _db.Comunicados.Any(c => c.Id == a.ComunicadoId));

    public List<RegistroPapelera> Listar()
    {
        var l = Sueltos().OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = l.Select(x => x.ComunicadoId).Distinct().ToList();
        var cods = _db.Comunicados.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Consecutivo);
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = x.Nombre,
            Subtitulo = $"{(cods.TryGetValue(x.ComunicadoId, out var k) ? k : "#" + x.ComunicadoId)} · {x.Clase}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.AdjuntosComunicado.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.AdjuntosComunicado.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            if (x.Clase == AdjuntoComunicado.Archivo) ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, x.Ruta);
            _db.AdjuntosComunicado.Remove(x);
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

public class ProveedorPapeleraCargasComunicados : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraCargasComunicados(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Cargas de comunicados";

    public List<RegistroPapelera> Listar()
    {
        var cargas = _db.CargasComunicados.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = cargas.Select(c => c.Id).ToList();
        var n = _db.Comunicados.IgnoreQueryFilters().AsNoTracking().Where(c => c.CargaComunicadosId != null && ids.Contains(c.CargaComunicadosId.Value) && c.Eliminado)
            .GroupBy(c => c.CargaComunicadosId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        return cargas.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id,
            Titulo = x.NombreArchivo + (string.IsNullOrWhiteSpace(x.Hoja) ? "" : " › " + x.Hoja),
            Subtitulo = $"{(n.TryGetValue(x.Id, out var k) ? k : 0)} comunicado(s) · cargado {x.Fecha:dd/MM/yyyy HH:mm}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var c = _db.CargasComunicados.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            foreach (var m in _db.Comunicados.IgnoreQueryFilters().Where(m => m.CargaComunicadosId == id && m.Eliminado && m.FechaEliminacion == c.FechaEliminacion))
            { m.Eliminado = false; m.FechaEliminacion = null; }
            c.Eliminado = false; c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        if (_db.CargasComunicados.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id) is null) return;
        var prov = new ProveedorPapeleraComunicados(_db, _env);
        foreach (var m in _db.Comunicados.IgnoreQueryFilters().Where(m => m.CargaComunicadosId == id && m.Eliminado).Select(m => m.Id).ToList())
            prov.EliminarDefinitivo(m);
        var c = _db.CargasComunicados.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null) { _db.CargasComunicados.Remove(c); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.CargasComunicados.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
