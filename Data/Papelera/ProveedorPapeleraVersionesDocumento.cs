using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de un archivo suelto de un documento (eliminado desde el menú del código o desde
// "Versiones anteriores"). Los archivos que se fueron con su carga los restaura la papelera de cargas.
public class ProveedorPapeleraVersionesDocumento : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraVersionesDocumento(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Archivos de documentos";

    // Solo los eliminados uno a uno (su carga sigue activa).
    private IQueryable<VersionDocumento> Sueltos() =>
        _db.VersionesDocumento.IgnoreQueryFilters().Where(v => v.Eliminado &&
            (v.CargaArchivosDocumentosId == null || _db.CargasArchivosDocumentos.Any(c => c.Id == v.CargaArchivosDocumentosId)));

    public List<RegistroPapelera> Listar()
    {
        var l = Sueltos().AsNoTracking().OrderByDescending(v => v.FechaEliminacion).ToList();
        var ids = l.Select(v => v.DocumentoMaestroId).Distinct().ToList();
        var cod = _db.DocumentosMaestro.IgnoreQueryFilters().AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionary(d => d.Id, d => d.Codigo);
        return l.Select(v => new RegistroPapelera
        {
            Modulo = Modulo, Id = v.Id, Titulo = v.NombreOriginal,
            Subtitulo = $"{(cod.TryGetValue(v.DocumentoMaestroId, out var c) && !string.IsNullOrWhiteSpace(c) ? c : "#" + v.DocumentoMaestroId)} · v{v.Version} · {v.Clase}",
            FechaEliminacion = v.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var v = _db.VersionesDocumento.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
        if (v is not null) { v.Eliminado = false; v.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var v = _db.VersionesDocumento.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
        if (v is not null)
        {
            _db.ControlesCambioVersion.RemoveRange(_db.ControlesCambioVersion.Where(x => x.VersionDocumentoId == id));
            try
            {
                var ruta = Path.Combine(_env.WebRootPath, v.RutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(ruta)) File.Delete(ruta);
            }
            catch { }
            _db.VersionesDocumento.Remove(v);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in Sueltos().Where(v => v.FechaEliminacion != null && v.FechaEliminacion < limite).Select(v => v.Id).ToList())
            EliminarDefinitivo(id);
    }
}
