using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Equipos;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Equipos: mismas columnas que la página (matriz importada + calibración vigente) y sus archivos:
// certificados de calibración (actual = el de la calibración vigente), mantenimientos, ficha técnica y fotos.
public sealed class FuenteEquipos : IFuenteDossier
{
    public string Clave => "equipos";
    public string Nombre => "Equipos";
    public string Icono => "equipos";
    public const string Certificados = "Certificados de calibración", Mantenimientos = "Mantenimientos", Ficha = "Ficha técnica", Fotos = "Fotografías";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var campos = await db.CamposEquipo.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync(ct);
        var cargas = await db.CargasEquipos.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var equipos = await db.Equipos.AsNoTracking().Where(e => !e.Eliminado).OrderBy(e => e.Nombre).ToListAsync(ct);
        var porId = equipos.ToDictionary(e => e.Id);
        var claveCampo = await db.CamposEquipo.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Clave, ct);
        var valores = (await db.ValoresCampoEquipo.AsNoTracking().ToListAsync(ct))
            .Where(v => porId.ContainsKey(v.EquipoId) && claveCampo.ContainsKey(v.CampoEquipoId))
            .GroupBy(v => v.EquipoId).ToDictionary(g => g.Key, g => g.GroupBy(v => claveCampo[v.CampoEquipoId]).ToDictionary(x => x.Key, x => x.First().Valor));
        var intervenciones = await db.IntervencionesEquipo.AsNoTracking().Where(i => !i.Eliminado).ToListAsync(ct);
        var vigente = intervenciones.Where(i => i.Tipo == TipoIntervencionEquipo.Calibracion).GroupBy(i => i.EquipoId)
            .ToDictionary(g => g.Key, g => EstadoEquipo.Vigente(g));

        static TipoCampoTabla TipoDe(TipoDatoCampoEnsayo t) => t switch
        {
            TipoDatoCampoEnsayo.Numero => TipoCampoTabla.Numero,
            TipoDatoCampoEnsayo.Fecha => TipoCampoTabla.Fecha,
            _ => TipoCampoTabla.Texto
        };
        IntervencionEquipo? V(int id) => vigente.GetValueOrDefault(id);
        var nativas = new List<ColumnaNativa>
        {
            new("Nombre", "Equipo", TipoCampoTabla.Texto, id => porId.TryGetValue(id, out var e) ? e.Nombre : null)
        };
        foreach (var c in campos)
        {
            var cc = c;
            nativas.Add(new ColumnaNativa("campo:" + cc.Clave, cc.Etiqueta, TipoDe(cc.TipoDato), id =>
                valores.TryGetValue(id, out var d) && d.TryGetValue(cc.Clave, out var v) && !string.IsNullOrWhiteSpace(v)
                    ? MotorTabla.Tipar(new CampoTabla { Tipo = TipoDe(cc.TipoDato) }, v) : null, true, "Excel"));
        }
        nativas.Add(new("Fecha", "Calibración", TipoCampoTabla.Fecha, id => V(id)?.Fecha));
        nativas.Add(new("Proxima", "Próxima", TipoCampoTabla.Fecha, id => V(id)?.ProximaFecha));
        nativas.Add(new("Estado", "Estado", TipoCampoTabla.Texto, id => porId.ContainsKey(id) ? EstadoEquipo.Estado(V(id)).Texto : null));
        nativas.Add(new("DiasProxima", "Días para la próxima calibración", TipoCampoTabla.Numero,
            id => V(id)?.ProximaFecha is DateTime p ? (object)(double)(int)(p.Date - DateTime.Today).TotalDays : null, false));
        nativas.Add(new("Origen", "Origen", TipoCampoTabla.Texto, id => porId.TryGetValue(id, out var e)
            ? (e.CargaEquiposId is int c && cargas.TryGetValue(c, out var k) ? $"Excel · {k.NombreArchivo}" : "Manual") : null));
        nativas.Add(new("Certificado", "Certificado vigente", TipoCampoTabla.Texto, id => V(id)?.NumeroCertificado));
        var motor = await MotorTabla.CargarLecturaAsync(db, "equipos", nativas, equipos.Select(e => (e.Id, e.Nombre)));

        var elementos = equipos.ToDictionary(e => e.Id, e => new ElementoDossier { Id = e.Id });
        foreach (var i in intervenciones.Where(i => !string.IsNullOrWhiteSpace(i.RutaCertificado)).OrderByDescending(i => i.Fecha).ThenByDescending(i => i.Id))
        {
            if (!elementos.TryGetValue(i.EquipoId, out var el)) continue;
            var cal = i.Tipo == TipoIntervencionEquipo.Calibracion;
            var nombre = $"{i.Fecha:yyyy-MM-dd} " + (string.IsNullOrWhiteSpace(i.NombreCertificado)
                ? (string.IsNullOrWhiteSpace(i.NumeroCertificado) ? Path.GetFileName(i.RutaCertificado!) : i.NumeroCertificado + Path.GetExtension(i.RutaCertificado!))
                : i.NombreCertificado);
            el.Archivos.Add(new ArchivoDossier(cal ? Certificados : Mantenimientos, nombre, i.RutaCertificado!,
                Actual: !cal || vigente.GetValueOrDefault(i.EquipoId)?.Id == i.Id));
        }
        foreach (var a in await db.ArchivosEquipo.AsNoTracking().Where(a => !a.Eliminado).OrderBy(a => a.Id).ToListAsync(ct))
            if (elementos.TryGetValue(a.EquipoId, out var el)) el.Archivos.Add(new ArchivoDossier(Ficha, a.NombreOriginal, a.RutaArchivo));
        foreach (var f in await db.FotosEquipo.AsNoTracking().OrderBy(f => f.Id).ToListAsync(ct))
            if (elementos.TryGetValue(f.EquipoId, out var el)) el.Archivos.Add(new ArchivoDossier(Fotos, Path.GetFileName(f.RutaArchivo), f.RutaArchivo));

        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = equipos.Select(e => elementos[e.Id]).ToList(), ColumnaNombre = "Nombre",
            Clases = new() { Certificados, Mantenimientos, Ficha, Fotos }, TieneVersiones = true
        };
    }
}
