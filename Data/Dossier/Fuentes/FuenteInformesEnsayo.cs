using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Ensayos: un elemento por informe de laboratorio (número, fecha, tipo y torres de sus resultados)
// con los archivos del informe. Grupo = tipo de ensayo. La tabla no tiene Campos propios en la app:
// se arma con columnas fijas (tabla de solo lectura "dossier:informes").
public sealed class FuenteInformesEnsayo : IFuenteDossier
{
    public string Clave => "informes";
    public string Nombre => "Informes de ensayo";
    public string Icono => "ensayos";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var tipos = await db.TiposEnsayo.AsNoTracking().Where(t => !t.Eliminado).ToDictionaryAsync(t => t.Id, ct);
        var grupos = await db.GruposEnsayo.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Nombre, ct);
        var informes = (await db.Informes.AsNoTracking().ToListAsync(ct)).Where(i => tipos.ContainsKey(i.TipoEnsayoId))
            .OrderBy(i => tipos[i.TipoEnsayoId].Nombre).ThenBy(i => i.Fecha ?? DateTime.MaxValue).ThenBy(i => i.Numero).ToList();
        var porId = informes.ToDictionary(i => i.Id);
        var torres = await db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).ToDictionaryAsync(t => t.Id, t => t.Codigo, ct);
        var resultados = (await db.ResultadosEnsayo.AsNoTracking().Where(r => !r.Eliminado && r.InformeId != null)
                .Select(r => new { InformeId = r.InformeId!.Value, r.ElementoTrazadoId, r.ElementoTexto }).ToListAsync(ct))
            .GroupBy(r => r.InformeId).ToDictionary(g => g.Key, g => g.ToList());

        Informe? I(int id) => porId.GetValueOrDefault(id);
        string? Tipo(int id) => I(id) is { } i && tipos.TryGetValue(i.TipoEnsayoId, out var t) ? t.Nombre : null;
        var nativas = new[]
        {
            new ColumnaNativa("Numero", "Informe", TipoCampoTabla.Texto, id => I(id)?.Numero),
            new ColumnaNativa("Fecha", "Fecha", TipoCampoTabla.Fecha, id => I(id)?.Fecha),
            new ColumnaNativa("Tipo", "Tipo de ensayo", TipoCampoTabla.Texto, id => Tipo(id)),
            new ColumnaNativa("Grupo", "Grupo", TipoCampoTabla.Texto, id => I(id) is { } i && tipos.TryGetValue(i.TipoEnsayoId, out var t) && grupos.TryGetValue(t.GrupoEnsayoId, out var g) ? g : null),
            new ColumnaNativa("Resultados", "Resultados", TipoCampoTabla.Numero, id => (double)(resultados.TryGetValue(id, out var l) ? l.Count : 0)),
            new ColumnaNativa("Elementos", "Torres / elementos", TipoCampoTabla.Texto, id => resultados.TryGetValue(id, out var l)
                ? string.Join(", ", l.Select(r => r.ElementoTrazadoId is int t && torres.TryGetValue(t, out var c) ? c : r.ElementoTexto)
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                : null),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:informes", nativas, informes.Select(i => (i.Id, i.Numero)));

        var ids = informes.Select(i => i.Id).ToList();
        var archivos = (await db.ArchivosInforme.AsNoTracking().Where(a => ids.Contains(a.InformeId)).OrderBy(a => a.Id).ToListAsync(ct))
            .GroupBy(a => a.InformeId).ToDictionary(g => g.Key, g => g.ToList());
        var elementos = informes.Select(i =>
        {
            var el = new ElementoDossier { Id = i.Id, Grupo = Tipo(i.Id) };
            if (archivos.TryGetValue(i.Id, out var l))
                foreach (var a in l) el.Archivos.Add(new ArchivoDossier("Informe", a.NombreOriginal, a.RutaArchivo));
            return el;
        }).ToList();

        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Numero", NombreGrupo = "Tipo de ensayo",
            Clases = new() { "Informe" }
        };
    }
}
