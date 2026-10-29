using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Papelera;

// Papelera de los campos creados o traídos de Excel en cualquier tabla (panel "Campos").
// El borrado definitivo elimina también sus valores y formatos (Cascade).
public class ProveedorPapeleraCamposTabla : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCamposTabla(AppDbContext db) { _db = db; }

    public string Modulo => "Campos de tablas";

    public List<RegistroPapelera> Listar() =>
        _db.CamposTabla.AsNoTracking().Where(c => c.Eliminado).OrderByDescending(c => c.FechaEliminacion).ToList()
            .Select(c => new RegistroPapelera { Modulo = Modulo, Id = c.Id, Titulo = c.Nombre, Subtitulo = "Tabla: " + c.Tabla, FechaEliminacion = c.FechaEliminacion })
            .ToList();

    public void Restaurar(int id)
    {
        var c = _db.CamposTabla.FirstOrDefault(x => x.Id == id);
        if (c is not null)
        {
            // Si mientras tanto se creó otro con el mismo nombre, el restaurado se renombra.
            if (_db.CamposTabla.Any(x => x.Tabla == c.Tabla && !x.Eliminado && x.Nombre.ToLower() == c.Nombre.ToLower()))
                c.Nombre += " (restaurado)";
            c.Eliminado = false; c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.CamposTabla.FirstOrDefault(x => x.Id == id);
        if (c is not null) { _db.CamposTabla.Remove(c); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.CamposTabla.Where(c => c.Eliminado && c.FechaEliminacion != null && c.FechaEliminacion < limite).ToList();
        if (vencidos.Count == 0) return;
        _db.CamposTabla.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
