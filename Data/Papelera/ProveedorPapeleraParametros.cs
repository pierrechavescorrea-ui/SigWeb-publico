using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de los parámetros (sub actividades) de cada actividad. Al eliminar solo se marca
// Eliminado: sus registros y adjuntos se conservan y vuelven al restaurar. El borrado
// definitivo elimina también sus registros (Cascade).
public class ProveedorPapeleraParametros : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraParametros(AppDbContext db) { _db = db; }

    public string Modulo => "Parámetros de actividad";

    public List<RegistroPapelera> Listar()
    {
        var actividades = _db.Actividades.AsNoTracking().ToDictionary(a => a.Id, a => a.Nombre);
        return _db.ParametrosActividad.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = string.IsNullOrWhiteSpace(x.Codigo) ? x.Nombre : $"{x.Codigo} - {x.Nombre}",
                Subtitulo = actividades.TryGetValue(x.ActividadId, out var n) ? n : "",
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var p = _db.ParametrosActividad.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (p is null) return avisos;
        var a = _db.Actividades.AsNoTracking().FirstOrDefault(x => x.Id == p.ActividadId);
        if (a is null || a.Eliminado)
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "grave",
                Mensaje = "La actividad de este parámetro está en la Papelera.",
                Solucion = "Restaura primero la actividad.",
                Menus = new() { "Actividades", "Matriz PIE" }
            });
        if (_db.ParametrosActividad.AsNoTracking().Any(x => x.ActividadId == p.ActividadId && x.Nombre.ToLower() == p.Nombre.ToLower()))
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "atencion",
                Mensaje = $"Ya existe otro parámetro \"{p.Nombre}\" en la misma actividad.",
                Solucion = "Restaura y luego renombra uno de los dos desde Actividades.",
                Menus = new() { "Actividades", "Matriz PIE" }
            });
        return avisos;
    }

    public void Restaurar(int id)
    {
        var x = _db.ParametrosActividad.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            x.Eliminado = false;
            x.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ParametrosActividad.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            _db.ParametrosActividad.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.ParametrosActividad.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .ToList();
        if (vencidos.Count == 0) return;
        _db.ParametrosActividad.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
