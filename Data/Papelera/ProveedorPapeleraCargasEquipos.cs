using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las cargas (Excel) de Equipos. Eliminar la carga manda a la Papelera sus calibraciones y
// los equipos que solo existían por ella (misma FechaEliminacion); restaurarla los devuelve. El borrado
// definitivo los borra (los equipos con fotos u otras intervenciones nunca se marcaron: se conservan).
public class ProveedorPapeleraCargasEquipos : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCargasEquipos(AppDbContext db) { _db = db; }

    public string Modulo => "Cargas de equipos";

    public List<RegistroPapelera> Listar()
    {
        var cargas = _db.CargasEquipos.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = cargas.Select(c => c.Id).ToList();
        var eq = _db.Equipos.AsNoTracking().Where(e => e.CargaEquiposId != null && ids.Contains(e.CargaEquiposId.Value) && e.Eliminado)
            .GroupBy(e => e.CargaEquiposId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        var ca = _db.IntervencionesEquipo.IgnoreQueryFilters().AsNoTracking().Where(i => i.CargaEquiposId != null && ids.Contains(i.CargaEquiposId.Value) && i.Eliminado)
            .GroupBy(i => i.CargaEquiposId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        return cargas.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id,
            Titulo = x.NombreArchivo + (string.IsNullOrWhiteSpace(x.Hoja) ? "" : " › " + x.Hoja),
            Subtitulo = $"{eq.GetValueOrDefault(x.Id)} equipo(s) · {ca.GetValueOrDefault(x.Id)} calibración(es) · cargado {x.Fecha:dd/MM/yyyy HH:mm}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var c = _db.CargasEquipos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            foreach (var i in _db.IntervencionesEquipo.IgnoreQueryFilters().Where(i => i.CargaEquiposId == id && i.Eliminado && i.FechaEliminacion == c.FechaEliminacion))
            { i.Eliminado = false; i.FechaEliminacion = null; }
            foreach (var e in _db.Equipos.Where(e => e.CargaEquiposId == id && e.Eliminado && e.FechaEliminacion == c.FechaEliminacion))
            { e.Eliminado = false; e.FechaEliminacion = null; }
            c.Eliminado = false; c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.CargasEquipos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is null) return;
        _db.IntervencionesEquipo.RemoveRange(_db.IntervencionesEquipo.IgnoreQueryFilters().Where(i => i.CargaEquiposId == id && i.Eliminado));
        var equipos = _db.Equipos.Where(e => e.CargaEquiposId == id && e.Eliminado && e.FechaEliminacion == c.FechaEliminacion).ToList();
        var ids = equipos.Select(e => e.Id).ToList();
        _db.ValoresCampoEquipo.RemoveRange(_db.ValoresCampoEquipo.Where(v => ids.Contains(v.EquipoId)));
        _db.Equipos.RemoveRange(equipos);
        _db.CargasEquipos.Remove(c);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.CargasEquipos.IgnoreQueryFilters()
                     .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
