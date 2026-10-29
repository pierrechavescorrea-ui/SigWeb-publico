using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera del Listado maestro de documentos. Eliminar solo marca la fila; el borrado
// definitivo quita también sus valores de columnas (ValorCampoTabla de la tabla "documentos")
// y sus versiones con los archivos.
public class ProveedorPapeleraDocumentosMaestro : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public const string Tabla = "documentos";

    public ProveedorPapeleraDocumentosMaestro(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Documentos";

    public List<RegistroPapelera> Listar() =>
        _db.DocumentosMaestro.IgnoreQueryFilters().AsNoTracking()
            // Los que se fueron con su carga los muestra (y restaura) la papelera de cargas.
            .Where(x => x.Eliminado && (x.CargaDocumentosId == null || _db.CargasDocumentos.Any(c => c.Id == x.CargaDocumentosId)))
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = string.IsNullOrWhiteSpace(x.Codigo) ? $"Documento #{x.Id}" : x.Codigo!,
                Subtitulo = "Listado maestro de documentos" + (string.IsNullOrWhiteSpace(x.TipoDocumental) ? "" : " · " + x.TipoDocumental),
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();

    public void Restaurar(int id)
    {
        var x = _db.DocumentosMaestro.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
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
        Borrar(_db, _env, new[] { id });
        _db.ChangeTracker.Clear();
    }

    // Borra para siempre documentos con sus valores, versiones y archivos (también lo usa la papelera de cargas).
    public static void Borrar(AppDbContext db, IWebHostEnvironment env, IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0) return;
        var campos = db.CamposTabla.Where(c => c.Tabla == Tabla).Select(c => c.Id).ToList();
        db.ValoresCampoTabla.RemoveRange(db.ValoresCampoTabla.Where(v => ids.Contains(v.FilaId) && campos.Contains(v.CampoTablaId)));
        var versiones = db.VersionesDocumento.IgnoreQueryFilters().Where(v => ids.Contains(v.DocumentoMaestroId)).ToList();
        foreach (var v in versiones) BorrarArchivo(env, v.RutaArchivo);
        db.VersionesDocumento.RemoveRange(versiones);
        db.DocumentosMaestro.RemoveRange(db.DocumentosMaestro.IgnoreQueryFilters().Where(d => ids.Contains(d.Id)));
        db.SaveChanges();
    }

    private static void BorrarArchivo(IWebHostEnvironment env, string? rutaWeb)
    {
        if (string.IsNullOrWhiteSpace(rutaWeb)) return;
        try
        {
            var ruta = Path.Combine(env.WebRootPath, rutaWeb.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch { /* si no se puede borrar el archivo, el registro igual se quita */ }
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.DocumentosMaestro.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .Select(x => x.Id).ToList();
        Borrar(_db, _env, vencidos);
        _db.ChangeTracker.Clear();
    }
}
