using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de Indicadores: el indicador se va con sus mediciones y los soportes de ellas
// (misma marca de tiempo); los soportes sueltos tienen su propio proveedor.
public class ProveedorPapeleraIndicadores : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraIndicadores(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Indicadores";

    public List<RegistroPapelera> Listar() =>
        _db.Indicadores.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = x.Nombre,
                Subtitulo = $"{SigWeb.Data.Indicadores.CalculoIndicadores.NombreFuente(x.Fuente)} · {x.Responsable}",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public void Restaurar(int id)
    {
        var ind = _db.Indicadores.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (ind is not null)
        {
            var med = _db.MedicionesIndicador.IgnoreQueryFilters().Where(m => m.IndicadorId == id && m.Eliminado && m.FechaEliminacion == ind.FechaEliminacion).ToList();
            var idsMed = med.Select(m => m.Id).ToList();
            foreach (var a in _db.ArchivosMedicionIndicador.IgnoreQueryFilters().Where(a => idsMed.Contains(a.MedicionIndicadorId) && a.Eliminado && a.FechaEliminacion == ind.FechaEliminacion))
            { a.Eliminado = false; a.FechaEliminacion = null; }
            foreach (var m in med) { m.Eliminado = false; m.FechaEliminacion = null; }
            ind.Eliminado = false; ind.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var ind = _db.Indicadores.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (ind is not null)
        {
            var idsMed = _db.MedicionesIndicador.IgnoreQueryFilters().Where(m => m.IndicadorId == id).Select(m => m.Id).ToList();
            foreach (var a in _db.ArchivosMedicionIndicador.IgnoreQueryFilters().Where(a => idsMed.Contains(a.MedicionIndicadorId)).ToList())
                ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, a.RutaArchivo);
            _db.Indicadores.Remove(ind);   // mediciones y soportes en cascada
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.Indicadores.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraArchivosIndicador : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraArchivosIndicador(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Soportes de indicadores";

    // Los que se fueron con su indicador aparecen (y vuelven) con él.
    private IQueryable<ArchivoMedicionIndicador> Sueltos() =>
        _db.ArchivosMedicionIndicador.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.Eliminado && _db.MedicionesIndicador.Any(m => m.Id == a.MedicionIndicadorId));

    public List<RegistroPapelera> Listar()
    {
        var l = Sueltos().OrderByDescending(x => x.FechaEliminacion).ToList();
        var idsMed = l.Select(x => x.MedicionIndicadorId).Distinct().ToList();
        var med = _db.MedicionesIndicador.AsNoTracking().Where(m => idsMed.Contains(m.Id)).ToDictionary(m => m.Id);
        var idsInd = med.Values.Select(m => m.IndicadorId).Distinct().ToList();
        var ind = _db.Indicadores.AsNoTracking().Where(i => idsInd.Contains(i.Id)).ToDictionary(i => i.Id, i => i.Nombre);
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = x.NombreOriginal,
            Subtitulo = med.TryGetValue(x.MedicionIndicadorId, out var m)
                ? $"{(ind.TryGetValue(m.IndicadorId, out var n) ? n : "Indicador")} · {SigWeb.Data.Indicadores.CalculoIndicadores.Mes(m.Anio, m.Mes)}"
                : "Indicador",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.ArchivosMedicionIndicador.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ArchivosMedicionIndicador.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, x.RutaArchivo);
            _db.ArchivosMedicionIndicador.Remove(x);
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
