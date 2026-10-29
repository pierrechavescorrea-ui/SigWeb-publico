using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera para los archivos (planos, fotos) adjuntos directamente a un
// componente (una pata), como el "Plano por pata de cimentación".
public class ProveedorPapeleraArchivosComponente : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraArchivosComponente(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public string Modulo => "Planos y archivos de patas";

    public List<RegistroPapelera> Listar()
    {
        var componentes = _db.Componentes.AsNoTracking().ToDictionary(c => c.Id, c => c);
        var torres = _db.ElementosTrazado.AsNoTracking().ToDictionary(t => t.Id, t => t.Codigo);

        return _db.ArchivosComponente.AsNoTracking()
            .Where(a => a.Eliminado)
            .OrderByDescending(a => a.FechaEliminacion)
            .ToList()
            .Select(a =>
            {
                string subtitulo = "";
                if (componentes.TryGetValue(a.ComponenteId, out var c))
                {
                    var nombreTorre = torres.TryGetValue(c.ElementoTrazadoId, out var codigo) ? $"Torre {codigo} — " : "";
                    subtitulo = $"{nombreTorre}{c.Nombre}";
                }
                return new RegistroPapelera
                {
                    Modulo = Modulo,
                    Id = a.Id,
                    Titulo = a.NombreOriginal,
                    Subtitulo = subtitulo,
                    FechaEliminacion = a.FechaEliminacion
                };
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var a = _db.ArchivosComponente.Find(id);
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
        var a = _db.ArchivosComponente.Find(id);
        if (a is not null)
        {
            BorrarArchivoFisico(a.RutaArchivo);
            _db.ArchivosComponente.Remove(a);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.ArchivosComponente
            .Where(a => a.Eliminado && a.FechaEliminacion != null && a.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        foreach (var a in vencidos)
        {
            BorrarArchivoFisico(a.RutaArchivo);
            _db.ArchivosComponente.Remove(a);
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
