using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las cargas (Excel) del Listado maestro de documentos. Eliminar una carga
// envía a la Papelera la carga y todas las filas que creó (misma FechaEliminacion);
// restaurarla devuelve esas filas. El borrado definitivo borra la carga y sus filas.
public class ProveedorPapeleraCargasDocumentos : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCargasDocumentos(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Cargas de documentos";

    public List<RegistroPapelera> Listar()
    {
        var cargas = _db.CargasDocumentos.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList();
        var ids = cargas.Select(c => c.Id).ToList();
        var filas = _db.DocumentosMaestro.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.CargaDocumentosId != null && ids.Contains(d.CargaDocumentosId.Value))
            .GroupBy(d => d.CargaDocumentosId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        return cargas.Select(x => new RegistroPapelera
        {
            Modulo = Modulo,
            Id = x.Id,
            Titulo = x.NombreArchivo + (string.IsNullOrWhiteSpace(x.Hoja) ? "" : " › " + x.Hoja),
            Subtitulo = $"{(filas.TryGetValue(x.Id, out var n) ? n : 0)} documento(s) · cargado {x.Fecha:dd/MM/yyyy HH:mm}",
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var c = _db.CargasDocumentos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            // Vuelven las filas que se fueron con la carga (no las que el usuario había borrado antes, una a una).
            foreach (var d in _db.DocumentosMaestro.IgnoreQueryFilters()
                         .Where(d => d.CargaDocumentosId == id && d.Eliminado && d.FechaEliminacion == c.FechaEliminacion))
            {
                d.Eliminado = false;
                d.FechaEliminacion = null;
            }
            c.Eliminado = false;
            c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.CargasDocumentos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            var docs = _db.DocumentosMaestro.IgnoreQueryFilters().Where(d => d.CargaDocumentosId == id).Select(d => d.Id).ToList();
            ProveedorPapeleraDocumentosMaestro.Borrar(_db, _env, docs);
            _db.CargasDocumentos.Remove(c);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.CargasDocumentos.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .Select(x => x.Id).ToList();
        foreach (var id in vencidos) EliminarDefinitivo(id);
    }
}
