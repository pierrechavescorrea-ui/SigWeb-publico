using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Documentos;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Listado maestro de documentos: mismas columnas que DocumentosMaestro; archivos por clase (Vigente,
// Editable, Obsoleta). Actuales = los de la revisión más reciente (misma regla que ActualesDe de la página).
public sealed class FuenteDocumentos : IFuenteDossier
{
    public string Clave => "documentos";
    public string Nombre => "Documentos";
    public string Icono => "documentos";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var docs = await db.DocumentosMaestro.AsNoTracking().Where(d => !d.Eliminado).OrderBy(d => d.Id).ToListAsync(ct);
        var porId = docs.ToDictionary(d => d.Id);
        var cargas = await db.CargasDocumentos.AsNoTracking().ToListAsync(ct);
        var nombreCarga = cargas.ToDictionary(c => c.Id, c => c.NombreArchivo + (string.IsNullOrWhiteSpace(c.Hoja) ? "" : " › " + c.Hoja));
        DocumentoMaestro? D(int id) => porId.TryGetValue(id, out var d) ? d : null;
        var nativas = new[]
        {
            new ColumnaNativa("Codigo", "Código", TipoCampoTabla.Texto, id => D(id)?.Codigo),
            new ColumnaNativa("TipoDocumental", "Tipo documental", TipoCampoTabla.Texto, id => D(id)?.TipoDocumental),
            new ColumnaNativa("Estado", "Estado", TipoCampoTabla.Texto, id => D(id)?.Estado),
            new ColumnaNativa("FechaCreacion", "Creado", TipoCampoTabla.Fecha, id => D(id)?.FechaCreacion, visiblePorDefecto: false),
            new ColumnaNativa("Carga", "Carga", TipoCampoTabla.Texto, id => D(id)?.CargaDocumentosId is int c && nombreCarga.TryGetValue(c, out var n) ? n : null, visiblePorDefecto: false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, MigracionDocumentos.Tabla, nativas, docs.Select(d => (d.Id, d.Codigo ?? $"#{d.Id}")));

        var ids = docs.Select(d => d.Id).ToList();
        var revisiones = (await db.RevisionesDocumento.AsNoTracking().Where(r => ids.Contains(r.DocumentoMaestroId)).ToListAsync(ct))
            .GroupBy(r => r.DocumentoMaestroId)
            .SelectMany(g => g.OrderByDescending(r => r.Fecha ?? DateTime.MinValue).ThenByDescending(r => r.Creada).ThenByDescending(r => r.Id).Select((r, i) => (r.Id, i)))
            .ToDictionary(x => x.Id, x => x.i);
        var versiones = (await db.VersionesDocumento.AsNoTracking().Where(v => ids.Contains(v.DocumentoMaestroId) && !v.Eliminado).ToListAsync(ct))
            .GroupBy(v => v.DocumentoMaestroId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(v => v.RevisionDocumentoId is int r && revisiones.TryGetValue(r, out var i) ? i : int.MaxValue)
                .ThenByDescending(v => v.FechaCarga).ThenByDescending(v => v.Id).ToList());

        var elementos = new List<ElementoDossier>();
        foreach (var d in docs)
        {
            var el = new ElementoDossier { Id = d.Id, Grupo = d.TipoDocumental };
            if (versiones.TryGetValue(d.Id, out var lista))
            {
                var actuales = Actuales(lista, ClaseArchivoDocumento.Vigente).Concat(Actuales(lista, ClaseArchivoDocumento.Editable)).Select(v => v.Id).ToHashSet();
                foreach (var v in lista)
                {
                    var actual = actuales.Contains(v.Id);
                    el.Archivos.Add(new ArchivoDossier(NombreClase(v.Clase), v.NombreOriginal, v.RutaArchivo, Actual: actual,
                        Carpeta: actual ? null : "Versiones anteriores"));
                }
            }
            elementos.Add(el);
        }

        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Codigo", NombreGrupo = "Tipo documental",
            Clases = new() { "Vigente", "Editable", "Obsoleta" }, TieneVersiones = true
        };
    }

    // Igual que DocumentosMaestro.ActualesDe (lista en el orden de la trazabilidad).
    private static List<VersionDocumento> Actuales(List<VersionDocumento> todos, ClaseArchivoDocumento clase)
    {
        var l = todos.Where(v => v.Clase == clase).ToList();
        if (l.Count == 0) return l;
        if (l[0].RevisionDocumentoId is int rev) return l.Where(v => v.RevisionDocumentoId == rev).ToList();
        var ver = (l[0].Version ?? "").Trim();
        return l.Where(v => v.RevisionDocumentoId is null && string.Equals((v.Version ?? "").Trim(), ver, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static string NombreClase(ClaseArchivoDocumento c) => c switch
    {
        ClaseArchivoDocumento.Vigente => "Vigente",
        ClaseArchivoDocumento.Editable => "Editable",
        _ => "Obsoleta"
    };
}
