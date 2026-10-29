using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera para los adjuntos (PDF, fotos) de un parámetro/checklist de
// una torre. Antes se borraban de una vez al hacer clic en "Quitar";
// ahora se piden confirmación y solo se marcan Eliminado = true — el
// archivo físico solo se borra desde acá (o al vencer el plazo).
public class ProveedorPapeleraArchivosRegistroParametro : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraArchivosRegistroParametro(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public string Modulo => "Adjuntos de checklist";

    public List<RegistroPapelera> Listar()
    {
        var registros = _db.RegistrosParametro.AsNoTracking().ToList();
        var componentes = _db.Componentes.AsNoTracking().ToDictionary(c => c.Id, c => c.Nombre);
        var torres = _db.ElementosTrazado.AsNoTracking().ToDictionary(t => t.Id, t => t.Codigo);
        var parametros = _db.ParametrosActividad.IgnoreQueryFilters().AsNoTracking().ToDictionary(p => p.Id, p => p.Nombre);

        return _db.ArchivosRegistroParametro.AsNoTracking()
            .Where(a => a.Eliminado)
            .OrderByDescending(a => a.FechaEliminacion)
            .ToList()
            .Select(a =>
            {
                var registro = registros.FirstOrDefault(r => r.Id == a.RegistroParametroId);
                string subtitulo = "";
                if (registro is not null)
                {
                    var nombreComponente = componentes.TryGetValue(registro.ComponenteId, out var nc) ? nc : "";
                    var nombreParametro = parametros.TryGetValue(registro.ParametroActividadId, out var np) ? np : "";
                    var codigoTorre = _db.Componentes.AsNoTracking().Where(c => c.Id == registro.ComponenteId)
                        .Select(c => c.ElementoTrazadoId).FirstOrDefault();
                    var nombreTorre = torres.TryGetValue(codigoTorre, out var nt) ? $"Torre {nt} — " : "";
                    subtitulo = $"{nombreTorre}{nombreComponente} · {nombreParametro}";
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
        var a = _db.ArchivosRegistroParametro.Find(id);
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
        var a = _db.ArchivosRegistroParametro.Find(id);
        if (a is not null)
        {
            BorrarArchivoFisico(a.RutaArchivo);
            _db.ArchivosRegistroParametro.Remove(a);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.ArchivosRegistroParametro
            .Where(a => a.Eliminado && a.FechaEliminacion != null && a.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        foreach (var a in vencidos)
        {
            BorrarArchivoFisico(a.RutaArchivo);
            _db.ArchivosRegistroParametro.Remove(a);
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
