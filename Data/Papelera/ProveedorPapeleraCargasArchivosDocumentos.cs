using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las cargas de archivos del Listado maestro de documentos. Eliminar una carga envía a
// la Papelera la carga y sus archivos (misma FechaEliminacion); restaurarla los devuelve. El borrado
// definitivo borra los archivos del disco, sus registros y su control de cambios.
public class ProveedorPapeleraCargasArchivosDocumentos : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCargasArchivosDocumentos(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Cargas de archivos de documentos";

    public List<RegistroPapelera> Listar() =>
        _db.CargasArchivosDocumentos.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Origen + (string.IsNullOrWhiteSpace(x.Descripcion) ? "" : " · " + x.Descripcion),
                Subtitulo = $"{x.Archivos} archivo(s) · cargado {x.Fecha:dd/MM/yyyy HH:mm}",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public void Restaurar(int id)
    {
        var c = _db.CargasArchivosDocumentos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            foreach (var v in _db.VersionesDocumento.IgnoreQueryFilters()
                         .Where(v => v.CargaArchivosDocumentosId == id && v.Eliminado && v.FechaEliminacion == c.FechaEliminacion))
            {
                v.Eliminado = false;
                v.FechaEliminacion = null;
            }
            c.Eliminado = false;
            c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.CargasArchivosDocumentos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            var versiones = _db.VersionesDocumento.IgnoreQueryFilters().Where(v => v.CargaArchivosDocumentosId == id).ToList();
            var ids = versiones.Select(v => v.Id).ToList();
            _db.ControlesCambioVersion.RemoveRange(_db.ControlesCambioVersion.Where(x => ids.Contains(x.VersionDocumentoId)));
            foreach (var v in versiones) BorrarArchivo(v.RutaArchivo);
            _db.VersionesDocumento.RemoveRange(versiones);
            _db.CargasArchivosDocumentos.Remove(c);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    private void BorrarArchivo(string? rutaWeb)
    {
        if (string.IsNullOrWhiteSpace(rutaWeb)) return;
        try
        {
            var ruta = Path.Combine(_env.WebRootPath, rutaWeb.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch { /* si no se puede borrar el archivo, el registro igual se quita */ }
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.CargasArchivosDocumentos.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .Select(x => x.Id).ToList();
        foreach (var id in vencidos) EliminarDefinitivo(id);
    }
}
