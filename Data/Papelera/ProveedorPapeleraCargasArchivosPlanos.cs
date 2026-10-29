using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de las cargas de archivos del listado maestro de planos: eliminar la carga manda sus
// archivos a la Papelera (misma FechaEliminacion); restaurarla los devuelve; el borrado definitivo
// borra los archivos del disco (los vínculos con componentes se borran en cascada).
public class ProveedorPapeleraCargasArchivosPlanos : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCargasArchivosPlanos(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Cargas de archivos de planos";

    public List<RegistroPapelera> Listar() =>
        _db.CargasArchivosPlanos.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id,
                Titulo = x.Origen + (string.IsNullOrWhiteSpace(x.Descripcion) ? "" : " · " + x.Descripcion),
                Subtitulo = $"{x.Archivos} archivo(s) · cargado {x.Fecha:dd/MM/yyyy HH:mm}",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public void Restaurar(int id)
    {
        var c = _db.CargasArchivosPlanos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            foreach (var a in _db.ArchivosPlanoMaestro.Where(a => a.CargaArchivosPlanosId == id && a.Eliminado && a.FechaEliminacion == c.FechaEliminacion))
            { a.Eliminado = false; a.FechaEliminacion = null; }
            c.Eliminado = false; c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.CargasArchivosPlanos.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            var archivos = _db.ArchivosPlanoMaestro.Where(a => a.CargaArchivosPlanosId == id).ToList();
            foreach (var a in archivos) BorrarArchivo(a.RutaArchivo);
            _db.ArchivosPlanoMaestro.RemoveRange(archivos);
            _db.CargasArchivosPlanos.Remove(c);
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
        catch { }
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.CargasArchivosPlanos.IgnoreQueryFilters()
                     .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
