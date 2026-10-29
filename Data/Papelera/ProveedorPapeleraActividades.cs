using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las actividades (Torre / Pata / Tiro). Al eliminar se marca
// Eliminado y Activo = false; al restaurar vuelve a quedar activa.
public class ProveedorPapeleraActividades : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraActividades(AppDbContext db) { _db = db; }

    public string Modulo => "Actividades";

    public List<RegistroPapelera> Listar()
    {
        return _db.Actividades.AsNoTracking()
            .Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Nombre,
                Subtitulo = x.Ambito switch { AmbitoActividad.Torre => "Torre", AmbitoActividad.Pata => "Pata", _ => "Tiro (Tendido)" },
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var a = _db.Actividades.AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (a is null) return avisos;
        var igual = _db.Actividades.AsNoTracking().Any(x => !x.Eliminado && x.Ambito == a.Ambito && x.Nombre.ToLower() == a.Nombre.ToLower());
        if (igual)
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "atencion",
                Mensaje = $"Ya existe otra actividad \"{a.Nombre}\" en el mismo grupo: quedarían dos con el mismo nombre.",
                Solucion = "Restaura y luego renombra una de las dos desde Actividades.",
                Menus = new() { "Actividades", "Hoja de vida", "Matriz PIE" }
            });
        if (a.TipoEnsayoId != null)
        {
            var tipo = _db.TiposEnsayo.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(t => t.Id == a.TipoEnsayoId);
            if (tipo is null || tipo.Eliminado)
                avisos.Add(new AvisoRestauracion
                {
                    Nivel = "grave",
                    Mensaje = "Esta actividad está enlazada a un tipo de ensayo que está en la Papelera.",
                    Solucion = "Restaura primero el tipo de ensayo (módulo \"Tipos de ensayo\").",
                    Menus = new() { "Ensayos", "Ficha de pata (Trazado)" }
                });
        }
        return avisos;
    }

    public void Restaurar(int id)
    {
        var x = _db.Actividades.FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            x.Eliminado = false;
            x.FechaEliminacion = null;
            x.Activo = true;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.Actividades.FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            _db.Actividades.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.Actividades
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .ToList();
        if (vencidos.Count == 0) return;
        _db.Actividades.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
