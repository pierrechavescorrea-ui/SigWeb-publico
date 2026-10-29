using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Cada PDF vinculado a una fila del listado maestro se puede quitar
// individualmente (ej. se cargó por error, o duplicado) sin borrar el
// resto de hojas de ese mismo plano. Igual que ProveedorPapeleraArchivosComponente.
public class ProveedorPapeleraArchivosPlanoMaestro : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraArchivosPlanoMaestro(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public string Modulo => "Archivos del listado de planos";

    public List<RegistroPapelera> Listar()
    {
        var planos = _db.PlanosMaestro.AsNoTracking().ToDictionary(p => p.Id, p => p.Codigo);

        // Los que se fueron con su carga los muestra (y restaura) la papelera de cargas.
        return _db.ArchivosPlanoMaestro.AsNoTracking()
            .Where(a => a.Eliminado && (a.CargaArchivosPlanosId == null || _db.CargasArchivosPlanos.Any(c => c.Id == a.CargaArchivosPlanosId)))
            .OrderByDescending(a => a.FechaEliminacion)
            .ToList()
            .Select(a => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = a.Id,
                Titulo = a.NombreOriginal,
                Subtitulo = planos.TryGetValue(a.PlanoMaestroId, out var codigo) ? codigo : "",
                FechaEliminacion = a.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var a = _db.ArchivosPlanoMaestro.Find(id);
        if (a is not null)
        {
            a.Eliminado = false;
            a.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var a = _db.ArchivosPlanoMaestro.Find(id);
        if (a is not null)
        {
            BorrarArchivoFisico(a.RutaArchivo);
            _db.ArchivosPlanoMaestro.Remove(a);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.ArchivosPlanoMaestro
            .Where(a => a.Eliminado && a.FechaEliminacion != null && a.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        foreach (var a in vencidos)
        {
            BorrarArchivoFisico(a.RutaArchivo);
            _db.ArchivosPlanoMaestro.Remove(a);
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
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
