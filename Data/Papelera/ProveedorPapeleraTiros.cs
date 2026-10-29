using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de los tiros de tendido. Al eliminar solo se marca Eliminado:
// las torres conservan su TiroId y los registros se conservan, así que al
// restaurar el tiro vuelve tal cual. El borrado definitivo deja las torres
// sin tiro (SetNull) y borra sus registros (Cascade).
public class ProveedorPapeleraTiros : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraTiros(AppDbContext db) { _db = db; }

    public string Modulo => "Tiros";

    public List<RegistroPapelera> Listar()
    {
        return _db.Tiros.AsNoTracking()
            .Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Nombre,
                Subtitulo = "Tiro (Tendido)",
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.Tiros.FirstOrDefault(e => e.Id == id);
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
        var x = _db.Tiros.FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var t in _db.ElementosTrazado.Where(e => e.TiroId == id)) t.TiroId = null;
            _db.Tiros.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.Tiros
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .Select(x => x.Id)
            .ToList();
        foreach (var id in vencidos) EliminarDefinitivo(id);
    }
}
