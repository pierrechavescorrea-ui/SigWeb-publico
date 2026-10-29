using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera para los "elementos" de una torre (patas, trinchos, cunetas,
// gaviones, etc.). Igual patrón que ProveedorPapeleraMateriales: al
// eliminar, solo se marca Eliminado = true; el borrado físico (con sus
// registros de checklist en cascada) ocurre desde la Papelera.
public class ProveedorPapeleraComponentes : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraComponentes(AppDbContext db)
    {
        _db = db;
    }

    public string Modulo => "Elementos de trazado";

    public List<RegistroPapelera> Listar()
    {
        var torres = _db.ElementosTrazado.AsNoTracking().ToDictionary(t => t.Id, t => t.Codigo);

        return _db.Componentes.AsNoTracking()
            .Where(c => c.Eliminado)
            .OrderByDescending(c => c.FechaEliminacion)
            .ToList()
            .Select(c => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = c.Id,
                Titulo = c.Nombre,
                Subtitulo = torres.TryGetValue(c.ElementoTrazadoId, out var codigo) ? $"Torre {codigo}" : "",
                FechaEliminacion = c.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var c = _db.Componentes.Find(id);
        if (c is not null)
        {
            c.Eliminado = false;
            c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.Componentes.Find(id);
        if (c is not null)
        {
            _db.Componentes.Remove(c); // el cascade en la BD borra también sus registros de checklist
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.Componentes
            .Where(c => c.Eliminado && c.FechaEliminacion != null && c.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        _db.Componentes.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}