using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Implementación de la papelera para el módulo Materiales.
// Sirve de plantilla: para darle papelera a un módulo nuevo, copia esta
// clase, cambia "Materiales" por el nombre del módulo y ajusta los
// nombres de campos (Titulo, Subtitulo) y el borrado de archivos si aplica.
public class ProveedorPapeleraMateriales : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraMateriales(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public string Modulo => "Materiales";

    public List<RegistroPapelera> Listar()
    {
        // Los que se fueron con su carga los muestra (y restaura) la papelera de cargas.
        return _db.Materiales.AsNoTracking()
            .Where(m => m.Eliminado && (m.CargaMaterialesId == null || _db.CargasMateriales.Any(c => c.Id == m.CargaMaterialesId)))
            .OrderByDescending(m => m.FechaEliminacion)
            .Select(m => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = m.Id,
                Titulo = m.NombreMaterial != "" ? m.NombreMaterial : "Material #" + m.Id,
                Subtitulo = m.Proveedor,
                FechaEliminacion = m.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var m = _db.Materiales.Find(id);
        if (m is not null)
        {
            m.Eliminado = false;
            m.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var m = _db.Materiales.Find(id);
        if (m is not null)
        {
            BorrarArchivosDelMaterial(m.Id, m.RutaRemisionPdf, m.RutaCertificadoPdf);
            _db.Materiales.Remove(m); // el cascade en la BD borra también sus fotos
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.Materiales
            .Where(m => m.Eliminado && m.FechaEliminacion != null && m.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        foreach (var m in vencidos)
        {
            BorrarArchivosDelMaterial(m.Id, m.RutaRemisionPdf, m.RutaCertificadoPdf);
            _db.Materiales.Remove(m);
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void BorrarArchivosDelMaterial(int materialId, string? rutaRemision, string? rutaCertificado)
    {
        var fotos = _db.FotosMateriales.Where(f => f.MaterialId == materialId).ToList();
        foreach (var f in fotos)
        {
            BorrarArchivoFisico(f.RutaArchivo);
        }
        // Adjuntos (también los que ya estaban en la Papelera); sus filas las borra el cascade.
        foreach (var a in _db.AdjuntosMaterial.IgnoreQueryFilters().Where(a => a.MaterialId == materialId).Select(a => a.RutaArchivo).ToList())
            BorrarArchivoFisico(a);
        BorrarArchivoFisico(rutaRemision);
        BorrarArchivoFisico(rutaCertificado);
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