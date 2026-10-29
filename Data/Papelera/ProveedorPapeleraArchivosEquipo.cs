using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de los anexos de la Ficha técnica de un equipo.
public class ProveedorPapeleraArchivosEquipo : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraArchivosEquipo(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Fichas técnicas de equipos";

    public List<RegistroPapelera> Listar()
    {
        var l = _db.ArchivosEquipo.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = l.Select(x => x.EquipoId).Distinct().ToList();
        var nombres = _db.Equipos.IgnoreQueryFilters().AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionary(e => e.Id, e => e.Nombre);
        return l.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = x.NombreOriginal,
            Subtitulo = nombres.TryGetValue(x.EquipoId, out var n) ? n : $"Equipo #{x.EquipoId}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.ArchivosEquipo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ArchivosEquipo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, x.RutaArchivo);
            _db.ArchivosEquipo.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.ArchivosEquipo.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
