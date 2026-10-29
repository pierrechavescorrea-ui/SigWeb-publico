using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Planos;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Listado maestro de planos: mismas columnas que PlanosMaestro; actuales = ArchivosPlanos.Actuales.
public sealed class FuentePlanos : IFuenteDossier
{
    public string Clave => "planos";
    public string Nombre => "Planos";
    public string Icono => "planos";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var planos = await db.PlanosMaestro.AsNoTracking().Where(p => !p.Eliminado).OrderBy(p => p.Codigo).ToListAsync(ct);
        var porId = planos.ToDictionary(p => p.Id);
        var grupos = await db.GruposPlano.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Nombre, ct);
        PlanoMaestro? P(int id) => porId.TryGetValue(id, out var p) ? p : null;
        string? Grupo(PlanoMaestro? p) => p?.GrupoPlanoId is int g && grupos.TryGetValue(g, out var n) ? n : null;
        var nativas = new[]
        {
            new ColumnaNativa("Codigo", "Código", TipoCampoTabla.Texto, id => P(id)?.Codigo),
            new ColumnaNativa("Titulo", "Título", TipoCampoTabla.Texto, id => P(id)?.Titulo),
            new ColumnaNativa("Fecha", "Fecha", TipoCampoTabla.Fecha, id => P(id)?.Fecha),
            new ColumnaNativa("RevisionVigente", "Rev. vigente", TipoCampoTabla.Texto, id => P(id)?.RevisionVigente),
            new ColumnaNativa("Estado", "Estado", TipoCampoTabla.Texto, id => P(id)?.Estado),
            new ColumnaNativa("Grupo", "Grupo", TipoCampoTabla.Texto, id => Grupo(P(id))),
            new ColumnaNativa("RevisionAnterior", "Rev. anterior", TipoCampoTabla.Texto, id => P(id)?.RevisionAnterior, false),
            new ColumnaNativa("Notas", "Notas", TipoCampoTabla.Texto, id => P(id)?.Notas, false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "planos", nativas,
            planos.Select(p => (p.Id, string.IsNullOrWhiteSpace(p.Titulo) ? p.Codigo : $"{p.Codigo} — {p.Titulo}")));

        var ids = planos.Select(p => p.Id).ToList();
        var archivos = (await db.ArchivosPlanoMaestro.AsNoTracking().Where(a => !a.Eliminado && ids.Contains(a.PlanoMaestroId)).ToListAsync(ct))
            .GroupBy(a => a.PlanoMaestroId).ToDictionary(g => g.Key, g => g.ToList());

        var elementos = new List<ElementoDossier>();
        foreach (var p in planos)
        {
            var el = new ElementoDossier { Id = p.Id, Grupo = Grupo(p) };
            if (archivos.TryGetValue(p.Id, out var lista))
            {
                var actuales = ArchivosPlanos.Actuales(lista);
                foreach (var a in actuales)
                    el.Archivos.Add(new ArchivoDossier("Vigente", a.NombreOriginal, a.RutaArchivo));
                foreach (var a in ArchivosPlanos.Anteriores(lista))
                    el.Archivos.Add(new ArchivoDossier(a.Clase == ClaseArchivoDocumento.Obsoleta ? "Obsoleta" : "Vigente", a.NombreOriginal, a.RutaArchivo,
                        Actual: false, Carpeta: "Versiones anteriores"));
            }
            elementos.Add(el);
        }

        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Codigo", NombreGrupo = "Grupo",
            Clases = new() { "Vigente", "Obsoleta" }, TieneVersiones = true
        };
    }
}
