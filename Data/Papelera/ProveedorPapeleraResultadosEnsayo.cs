using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera para los resultados de Ensayos. Cubre tanto el borrado de una
// fila individual (ya existente en EnsayosDatos.razor) como el de una
// carga completa (DialogoImportarEnsayo/EnsayosDatos: "Eliminar carga"):
// ambos casos solo marcan Eliminado = true, así que llegan aquí igual.
public class ProveedorPapeleraResultadosEnsayo : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraResultadosEnsayo(AppDbContext db)
    {
        _db = db;
    }

    public string Modulo => "Resultados de ensayos";

    public List<RegistroPapelera> Listar()
    {
        var tipos = _db.TiposEnsayo.AsNoTracking().ToDictionary(t => t.Id, t => t.Nombre);
        var torres = _db.ElementosTrazado.AsNoTracking().ToDictionary(t => t.Id, t => t.Codigo);

        return _db.ResultadosEnsayo.AsNoTracking()
            .Where(r => r.Eliminado)
            .OrderByDescending(r => r.FechaEliminacion)
            .ToList()
            .Select(r => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = r.Id,
                Titulo = (r.ElementoTrazadoId is int idTorre && torres.TryGetValue(idTorre, out var codigo) ? $"Torre {codigo}" : "Sin torre")
                    + (string.IsNullOrWhiteSpace(r.ElementoTexto) ? "" : $" — {r.ElementoTexto}"),
                Subtitulo = (tipos.TryGetValue(r.TipoEnsayoId, out var nombreTipo) ? nombreTipo : "Ensayo") + " · " + r.Origen,
                FechaEliminacion = r.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var r = _db.ResultadosEnsayo.Find(id);
        if (r is not null)
        {
            r.Eliminado = false;
            r.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var r = _db.ResultadosEnsayo.Find(id);
        if (r is not null)
        {
            _db.ResultadosEnsayo.Remove(r); // cascada: sus ValorCampoEnsayo y ResultadoEnsayoComponente se borran con él
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.ResultadosEnsayo
            .Where(r => r.Eliminado && r.FechaEliminacion != null && r.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        _db.ResultadosEnsayo.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
