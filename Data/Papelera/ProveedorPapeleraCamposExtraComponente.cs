using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera para los "datos adicionales de pata" (CampoExtraComponente):
// llegan aquí tanto los que el usuario borra a mano como los que el propio
// importador reemplaza automáticamente al cargar una tabla de torres
// nueva (ver DialogoDatosAdicionalesTorres.Importar).
public class ProveedorPapeleraCamposExtraComponente : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCamposExtraComponente(AppDbContext db)
    {
        _db = db;
    }

    public string Modulo => "Datos adicionales de patas";

    public List<RegistroPapelera> Listar()
    {
        var componentes = _db.Componentes.AsNoTracking().ToDictionary(c => c.Id, c => c);
        var torres = _db.ElementosTrazado.AsNoTracking().ToDictionary(t => t.Id, t => t.Codigo);

        return _db.CamposExtraComponente.AsNoTracking()
            .Where(c => c.Eliminado)
            .OrderByDescending(c => c.FechaEliminacion)
            .ToList()
            .Select(c =>
            {
                string titulo = c.Clave;
                if (componentes.TryGetValue(c.ComponenteId, out var componente))
                {
                    var codigo = torres.TryGetValue(componente.ElementoTrazadoId, out var cod) ? cod : "?";
                    titulo = $"Torre {codigo} — {componente.Nombre} — {c.Clave}";
                }

                return new RegistroPapelera
                {
                    Modulo = Modulo,
                    Id = c.Id,
                    Titulo = titulo,
                    Subtitulo = (string.IsNullOrWhiteSpace(c.Valor) ? "(sin valor)" : c.Valor) + " · " + c.Origen,
                    FechaEliminacion = c.FechaEliminacion
                };
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var c = _db.CamposExtraComponente.Find(id);
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
        var c = _db.CamposExtraComponente.Find(id);
        if (c is not null)
        {
            _db.CamposExtraComponente.Remove(c);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.CamposExtraComponente
            .Where(c => c.Eliminado && c.FechaEliminacion != null && c.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        _db.CamposExtraComponente.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
