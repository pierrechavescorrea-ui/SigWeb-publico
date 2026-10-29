using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Documentos;
using SigWeb.Data.ListadoMateriales;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Materiales: Campos de la tabla "materiales" + Ubicación y Adjuntos (igual que la página); adjuntos por
// clase (Remisión, Certificado, Registro fotográfico, Otros). Grupo = primera ubicación (torre/tiro).
public sealed class FuenteMateriales : IFuenteDossier
{
    public string Clave => "materiales";
    public string Nombre => "Materiales";
    public string Icono => "materiales";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var materiales = await db.Materiales.AsNoTracking().Where(m => !m.Eliminado).OrderBy(m => m.Id).ToListAsync(ct);
        var ids = materiales.Select(m => m.Id).ToList();
        var adjuntos = (await db.AdjuntosMaterial.AsNoTracking().Where(a => !a.Eliminado && ids.Contains(a.MaterialId)).ToListAsync(ct))
            .GroupBy(a => a.MaterialId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FechaCarga).ThenByDescending(a => a.Id).ToList());
        var ubicaciones = (await db.UbicacionesMaterial.AsNoTracking().Where(u => ids.Contains(u.MaterialId)).OrderBy(u => u.Id).ToListAsync(ct))
            .GroupBy(u => u.MaterialId).ToDictionary(g => g.Key, g => g.ToList());
        var lugares = LugaresMaterial.Cargar(db);
        List<UbicacionMaterial> U(int id) => ubicaciones.TryGetValue(id, out var l) ? l : new();
        var nativas = new[]
        {
            new ColumnaNativa("Ubicacion", "Ubicación", TipoCampoTabla.Texto, id => string.Join(", ", U(id).Select(u => lugares.Etiqueta(u)))),
            new ColumnaNativa("Adjuntos", "Adjuntos", TipoCampoTabla.Numero, id => (double)(adjuntos.TryGetValue(id, out var l) ? l.Count : 0)),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, MigracionMateriales.Tabla, nativas, materiales.Select(m => (m.Id, $"#{m.Id}")));

        // Igual que CampoTitulo de la página: la columna cuyo nombre diga "material"; si no, la primera.
        var titulo = motor.Ordenados.Where(c => c.Origen != OrigenCampoTabla.Nativo)
            .OrderByDescending(c => MigracionDocumentos.Normalizar(c.Nombre).Contains("material")).FirstOrDefault();

        var elementos = new List<ElementoDossier>();
        foreach (var m in materiales)
        {
            var ub = U(m.Id).FirstOrDefault();
            var el = new ElementoDossier { Id = m.Id, Grupo = ub is null ? null : lugares.Etiqueta(ub).Split(" · ")[0] };
            if (adjuntos.TryGetValue(m.Id, out var l))
                foreach (var a in l) el.Archivos.Add(new ArchivoDossier(a.Clase, a.NombreOriginal, a.RutaArchivo));
            elementos.Add(el);
        }

        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = titulo?.Clave ?? "Ubicacion",
            NombreGrupo = "Ubicación", Clases = AdjuntoMaterial.Clases.ToList()
        };
    }
}
