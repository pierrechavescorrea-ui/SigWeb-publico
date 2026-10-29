using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Busqueda;

// Buscador rápido (Ctrl + K): busca un texto en torres, planes de acción, riesgos, planos,
// documentos, materiales, equipos y tipos de ensayo, y devuelve a dónde ir. Lo usa el
// endpoint GET /api/buscar (js/navegacion.js). Solo lectura, sin seguimiento de cambios.
public static class BuscadorGlobal
{
    public sealed record Resultado(string Tipo, string Titulo, string? Detalle, string Url);

    private const int PorTipo = 6;

    public static async Task<List<Resultado>> BuscarAsync(AppDbContext db, string? texto)
    {
        var q = (texto ?? "").Trim();
        if (q.Length < 2) return new();
        var p = $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        var r = new List<Resultado>();

        var torres = await db.ElementosTrazado.AsNoTracking()
            .Where(t => !t.Eliminado && (EF.Functions.ILike(t.Codigo, p) || (t.Nombre != null && EF.Functions.ILike(t.Nombre, p))))
            .OrderBy(t => t.Codigo).Take(PorTipo).Select(t => new { t.Id, t.Codigo, t.Nombre }).ToListAsync();
        r.AddRange(torres.Select(t => new Resultado("Torre", t.Codigo, t.Nombre, $"trazado?torre={t.Id}")));

        var planes = await db.PlanesAccion.AsNoTracking()
            .Where(x => EF.Functions.ILike(x.Codigo, p) || EF.Functions.ILike(x.DescripcionHallazgo, p))
            .OrderByDescending(x => x.Fecha).Take(PorTipo).Select(x => new { x.Id, x.Codigo, x.DescripcionHallazgo }).ToListAsync();
        r.AddRange(planes.Select(x => new Resultado("Plan de acción", x.Codigo, Corto(x.DescripcionHallazgo), $"planes-accion/{x.Id}")));

        var versiones = await db.VersionesMatrizRiesgos.AsNoTracking().ToDictionaryAsync(v => v.Id, v => new { v.Codigo, v.Estado });
        var riesgos = await db.Riesgos.AsNoTracking()
            .Where(x => EF.Functions.ILike(x.Codigo, p) || EF.Functions.ILike(x.Descripcion, p))
            .OrderByDescending(x => x.VersionMatrizRiesgosId).ThenBy(x => x.Codigo).Take(PorTipo * 3)
            .Select(x => new { x.Id, x.Codigo, x.Descripcion, x.VersionMatrizRiesgosId }).ToListAsync();
        // Un riesgo existe en varias revisiones: se muestra el de la revisión vigente (o la más reciente).
        r.AddRange(riesgos.Where(x => versiones.ContainsKey(x.VersionMatrizRiesgosId))
            .GroupBy(x => x.Codigo)
            .Select(g => g.OrderByDescending(x => versiones[x.VersionMatrizRiesgosId].Estado == EstadoVersionMatriz.Vigente).ThenByDescending(x => x.VersionMatrizRiesgosId).First())
            .Take(PorTipo)
            .Select(x => new Resultado("Riesgo", $"{x.Codigo} ({versiones[x.VersionMatrizRiesgosId].Codigo})", Corto(x.Descripcion), $"riesgos/{x.Id}")));

        var planos = await db.PlanosMaestro.AsNoTracking()
            .Where(x => !x.Eliminado && (EF.Functions.ILike(x.Codigo, p) || EF.Functions.ILike(x.Titulo, p)))
            .OrderBy(x => x.Codigo).Take(PorTipo).Select(x => new { x.Codigo, x.Titulo }).ToListAsync();
        r.AddRange(planos.Select(x => new Resultado("Plano", x.Codigo, Corto(x.Titulo), $"planos?buscar={Uri.EscapeDataString(x.Codigo)}")));

        var docs = await db.DocumentosMaestro.AsNoTracking()
            .Where(x => x.Codigo != null && EF.Functions.ILike(x.Codigo, p))
            .OrderBy(x => x.Codigo).Take(PorTipo).Select(x => new { x.Codigo, x.TipoDocumental }).ToListAsync();
        r.AddRange(docs.Select(x => new Resultado("Documento", x.Codigo!, x.TipoDocumental, $"documentos?buscar={Uri.EscapeDataString(x.Codigo!)}")));

        var comunicados = await db.Comunicados.AsNoTracking()
            .Where(x => EF.Functions.ILike(x.Consecutivo, p) || (x.Descripcion != null && EF.Functions.ILike(x.Descripcion, p)))
            .OrderByDescending(x => x.Fecha).Take(PorTipo).Select(x => new { x.Consecutivo, x.Descripcion, x.Fecha }).ToListAsync();
        r.AddRange(comunicados.Select(x => new Resultado("Comunicado", x.Consecutivo.Trim(),
            Corto($"{x.Fecha:dd/MM/yyyy} · {x.Descripcion}"), $"comunicados?buscar={Uri.EscapeDataString(x.Consecutivo.Trim())}")));

        var materiales = await db.Materiales.AsNoTracking()
            .Where(x => !x.Eliminado && EF.Functions.ILike(x.NombreMaterial, p))
            .GroupBy(x => x.NombreMaterial).Select(g => g.Key).OrderBy(n => n).Take(PorTipo).ToListAsync();
        r.AddRange(materiales.Select(n => new Resultado("Material", n, null, $"materiales?buscar={Uri.EscapeDataString(n)}")));

        var equipos = await db.Equipos.AsNoTracking()
            .Where(x => !x.Eliminado && EF.Functions.ILike(x.Nombre, p))
            .OrderBy(x => x.Nombre).Take(PorTipo).Select(x => new { x.Id, x.Nombre }).ToListAsync();
        r.AddRange(equipos.Select(x => new Resultado("Equipo", x.Nombre, null, $"equipos/{x.Id}/hoja-de-vida")));

        var ensayos = await db.TiposEnsayo.AsNoTracking()
            .Where(x => EF.Functions.ILike(x.Nombre, p))
            .OrderBy(x => x.Nombre).Take(PorTipo).Select(x => new { x.Id, x.Nombre }).ToListAsync();
        r.AddRange(ensayos.Select(x => new Resultado("Ensayo", x.Nombre, null, $"ensayos/{x.Id}")));

        return r;
    }

    private static string? Corto(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        t = t.Replace('\n', ' ').Trim();
        return t.Length > 90 ? t[..87] + "…" : t;
    }
}
