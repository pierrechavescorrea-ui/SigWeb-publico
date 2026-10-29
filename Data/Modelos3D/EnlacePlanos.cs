using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Planos;
using SigWeb.Models;

namespace SigWeb.Data.Modelos3D;

// Relaciona cada plano citado por las piezas de un modelo con el listado maestro de Planos:
// si hay PDF adjunto de esa hoja, devuelve su ruta (enlace); si no, solo el nombre del plano.
// Misma regla que usa Modelos 3D. Clave de cada referencia: "codigo|hoja".
public static class EnlacePlanos
{
    public sealed record Referencia(string Codigo, int Hoja, string? Titulo);
    public sealed record Enlace(string Clave, string Texto, string? Url);

    public static string Clave(string codigo, int hoja) => $"{codigo}|{hoja}";

    // Referencia de una pieza: su plano propio, o el plano base del modelo con la hoja leída de la fuente ("…H14…").
    private static readonly Regex HojaEnFuente = new(@"H(\d+)");
    public static Referencia? DePieza(string? codigo, int hoja, string? titulo, string? planoBase, string? fuente)
    {
        if (!string.IsNullOrEmpty(codigo)) return new Referencia(codigo, hoja, titulo);
        if (!string.IsNullOrEmpty(planoBase) && HojaEnFuente.Match(fuente ?? "") is { Success: true } m)
            return new Referencia(planoBase, int.Parse(m.Groups[1].Value), null);
        return null;
    }

    public static async Task<List<Enlace>> ResolverAsync(AppDbContext db, IEnumerable<Referencia> referencias)
    {
        var refs = new Dictionary<string, Referencia>();
        foreach (var r in referencias) refs.TryAdd(Clave(r.Codigo, r.Hoja), r);
        if (refs.Count == 0) return new();

        var maestros = await db.PlanosMaestro.AsNoTracking().Where(p => !p.Eliminado).ToListAsync();
        var archivos = await db.ArchivosPlanoMaestro.AsNoTracking().Where(a => !a.Eliminado).ToListAsync();
        var lista = new List<Enlace>();
        foreach (var (clave, r) in refs)
        {
            var nombre = r.Hoja == 0 ? r.Codigo : $"{r.Codigo}-H{r.Hoja}";
            var pm = CoincidenciaPlanos.MejorCoincidencia(nombre, maestros, x => x.Codigo);
            // El maestro puede llevar prefijo (p. ej. "ACAD-" antes del código del plano).
            if (pm is null)
            {
                var nn = CoincidenciaPlanos.Normalizar(nombre);
                pm = maestros.Where(x => { var c = CoincidenciaPlanos.Normalizar(x.Codigo); return c == nn || c.EndsWith("-" + nn) || c.Contains("-" + nn + "-"); })
                             .OrderBy(x => x.Codigo.Length).FirstOrDefault();
            }
            ArchivoPlanoMaestro? arch = null;
            if (pm is not null)
            {
                var suyos = archivos.Where(a => a.PlanoMaestroId == pm.Id).ToList();
                var rx = new Regex($@"(^|-)(H|HOJA|HJ|SHEET)-?0*{r.Hoja}(-|$)");
                var rxNum = new Regex($@"-0*{r.Hoja}(-|$)");
                var cmaestro = CoincidenciaPlanos.Normalizar(pm.Codigo);
                // El maestro puede ser la hoja misma (código termina en -H14) o el juego completo.
                bool maestroEsHoja = r.Hoja != 0 && rx.IsMatch(cmaestro);
                arch = r.Hoja == 0 || maestroEsHoja ? suyos.FirstOrDefault()
                    : suyos.FirstOrDefault(a => rx.IsMatch(CoincidenciaPlanos.NormalizarNombreArchivo(a.NombreOriginal)))
                      ?? suyos.FirstOrDefault(a => rxNum.IsMatch(CoincidenciaPlanos.NormalizarNombreArchivo(a.NombreOriginal).Replace(cmaestro, "")));
                if (arch is null && suyos.Count == 1 && CoincidenciaPlanos.Normalizar(pm.Codigo) == CoincidenciaPlanos.Normalizar(nombre)) arch = suyos[0];
            }
            var texto = arch is not null ? arch.NombreOriginal : (r.Titulo is null ? nombre : $"{nombre} · {r.Titulo}");
            lista.Add(new Enlace(clave, texto, arch?.RutaArchivo));
        }
        return lista;
    }
}
