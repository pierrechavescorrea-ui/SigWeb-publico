using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

public class ProveedorPapeleraPlanoMaestro : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraPlanoMaestro(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public string Modulo => "Listado maestro de planos";

    public List<RegistroPapelera> Listar()
    {
        // Los que se fueron con su carga los muestra (y restaura) la papelera de cargas.
        return _db.PlanosMaestro.AsNoTracking()
            .Where(p => p.Eliminado && (p.CargaPlanosId == null || _db.CargasPlanos.Any(c => c.Id == p.CargaPlanosId)))
            .OrderByDescending(p => p.FechaEliminacion)
            .ToList()
            .Select(p => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = p.Id,
                Titulo = p.Codigo,
                Subtitulo = p.Titulo,
                FechaEliminacion = p.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var p = _db.PlanosMaestro.Find(id);
        if (p is not null)
        {
            p.Eliminado = false;
            p.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var p = _db.PlanosMaestro.Find(id);
        if (p is not null)
        {
            // Al borrar los ArchivoPlanoMaestro, la base de datos ya se encarga
            // de borrar en cascada los ComponentePlano que apuntaban a ellos.
            BorrarArchivosDelPlano(id);
            _db.PlanosMaestro.Remove(p);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.PlanosMaestro
            .Where(p => p.Eliminado && p.FechaEliminacion != null && p.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        foreach (var p in vencidos)
        {
            BorrarArchivosDelPlano(p.Id);
            _db.PlanosMaestro.Remove(p);
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    // Borra físicamente todos los PDF (incluidos los que ya estaban en su
    // propia papelera) de una fila que se está eliminando definitivamente.
    // Los vínculos ComponentePlano hacia esos archivos se borran en cascada
    // por la base de datos.
    private void BorrarArchivosDelPlano(int planoId)
    {
        var archivos = _db.ArchivosPlanoMaestro.Where(a => a.PlanoMaestroId == planoId).ToList();
        foreach (var a in archivos)
        {
            BorrarArchivoFisico(a.RutaArchivo);
        }
        _db.ArchivosPlanoMaestro.RemoveRange(archivos);
    }

    private void BorrarArchivoFisico(string? rutaWeb)
    {
        if (string.IsNullOrEmpty(rutaWeb)) return;
        try
        {
            var ruta = Path.Combine(_env.WebRootPath, rutaWeb.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch
        {
            // Archivo huérfano en disco, no crítico.
        }
    }
}
