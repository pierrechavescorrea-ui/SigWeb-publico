using Microsoft.EntityFrameworkCore;
using SigWeb.Data;

namespace SigWeb.Data.Ubicacion;

// Implementación para Materiales. Sirve de plantilla para los módulos
// que vengan: copia esta clase, cambia el nombre, la consulta y el texto
// de Detalle, y regístrala en Program.cs junto a esta.
public class ProveedorUbicacionMateriales : IProveedorUbicacion
{
    private readonly AppDbContext _db;

    public ProveedorUbicacionMateriales(AppDbContext db)
    {
        _db = db;
    }

    public string Modulo => "Materiales";

    // Materiales ubicados en este elemento/pata (Materiales › Ubicación o Hoja de vida › Materiales).
    public List<RegistroUbicacion> ObtenerPara(int componenteId)
    {
        var ids = _db.UbicacionesMaterial.AsNoTracking().Where(u => u.ComponenteId == componenteId).Select(u => u.MaterialId).Distinct().ToList();
        return _db.Materiales.AsNoTracking()
            .Where(m => !m.Eliminado && ids.Contains(m.Id))
            .OrderByDescending(m => m.Fecha)
            .Select(m => new RegistroUbicacion
            {
                Modulo = Modulo,
                Titulo = m.NombreMaterial != "" ? m.NombreMaterial : "Material #" + m.Id,
                Detalle = $"Material #{m.Id}"
            })
            .ToList();
    }
}