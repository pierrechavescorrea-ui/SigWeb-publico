using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de un adjunto suelto de un material (eliminado desde el menú de Adjuntos o "Ver todos").
public class ProveedorPapeleraAdjuntosMaterial : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraAdjuntosMaterial(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Adjuntos de materiales";

    // Los de un material en la Papelera se van (y vuelven) con él: no se listan aparte.
    private IQueryable<AdjuntoMaterial> Sueltos() =>
        _db.AdjuntosMaterial.IgnoreQueryFilters().Where(a => a.Eliminado && _db.Materiales.Any(m => m.Id == a.MaterialId));

    public List<RegistroPapelera> Listar() =>
        Sueltos().AsNoTracking().OrderByDescending(a => a.FechaEliminacion).ToList()
            .Select(a => new RegistroPapelera
            {
                Modulo = Modulo, Id = a.Id, Titulo = a.NombreOriginal,
                Subtitulo = $"Material #{a.MaterialId} · {a.Clase}", FechaEliminacion = a.FechaEliminacion
            }).ToList();

    public void Restaurar(int id)
    {
        var a = _db.AdjuntosMaterial.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
        if (a is not null) { a.Eliminado = false; a.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var a = _db.AdjuntosMaterial.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
        if (a is not null)
        {
            BorrarFisico(_env, a.RutaArchivo);
            _db.AdjuntosMaterial.Remove(a);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in Sueltos().Where(a => a.FechaEliminacion != null && a.FechaEliminacion < limite).Select(a => a.Id).ToList())
            EliminarDefinitivo(id);
    }

    public static void BorrarFisico(IWebHostEnvironment env, string? rutaWeb)
    {
        if (string.IsNullOrEmpty(rutaWeb)) return;
        try
        {
            var ruta = Path.Combine(env.WebRootPath, rutaWeb.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch { }
    }
}
