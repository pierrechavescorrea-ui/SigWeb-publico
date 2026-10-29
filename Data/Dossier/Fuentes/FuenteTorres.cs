using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Torres (Trazado): tabla de torres (TablaTorres: torre, coordenadas, columnas del Excel "x:…" y Campos)
// y archivos de la hoja de vida: los del elemento (ArchivoComponente) y la documentación de cada
// parámetro de actividad (ArchivoRegistroParametro). Carpeta = elemento / actividad.
public sealed class FuenteTorres : IFuenteDossier
{
    public string Clave => "torres";
    public string Nombre => "Torres";
    public string Icono => "trazado";
    public const string ClaseElemento = "Archivos del elemento";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var torres = (await db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).ToListAsync(ct))
            .OrderBy(t => OrdenNatural(t.Codigo)).ToList();
        var porId = torres.ToDictionary(t => t.Id);
        var idsT = torres.Select(t => t.Id).ToList();
        var comps = await db.Componentes.AsNoTracking().Where(c => !c.Eliminado && idsT.Contains(c.ElementoTrazadoId)).ToListAsync(ct);
        var compPorId = comps.ToDictionary(c => c.Id);
        var tiros = await db.Tiros.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        // Columnas del Excel de la tabla de torres (mismas claves que TablaTorres: "x:clave" y "x:Pata A | clave").
        static string K(string s) => (s ?? "").Trim().ToLowerInvariant();
        var patas = new Dictionary<(int, string), int>();
        foreach (var c in comps)
        {
            var n = c.Nombre.Trim();
            if (n.StartsWith("Pata ", StringComparison.OrdinalIgnoreCase) && n.Length == 6) patas[(c.ElementoTrazadoId, n[5].ToString().ToUpperInvariant())] = c.Id;
        }
        var extrasT = new Dictionary<(int, string), string>();
        foreach (var e in await db.CamposExtraTorre.AsNoTracking().Where(c => !c.Eliminado).ToListAsync(ct)) extrasT[(e.ElementoTrazadoId, K(e.Clave))] = e.Valor;
        var idsPatas = patas.Values.ToList();
        var extrasP = new Dictionary<(int, string), string>();
        foreach (var e in await db.CamposExtraComponente.AsNoTracking().Where(c => !c.Eliminado && idsPatas.Contains(c.ComponenteId)).ToListAsync(ct)) extrasP[(e.ComponenteId, K(e.Clave))] = e.Valor;
        var clavesX = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == "torres" && c.Origen == OrigenCampoTabla.Nativo && c.Clave.StartsWith("x:"))
            .Select(c => c.Clave).ToListAsync(ct);

        object? ValorX(int torreId, string claveX)
        {
            var key = claveX[2..];
            string v;
            var sep = key.IndexOf(" | ", StringComparison.Ordinal);
            if (key.StartsWith("Pata ", StringComparison.OrdinalIgnoreCase) && sep == 6)
                v = patas.TryGetValue((torreId, key[5].ToString().ToUpperInvariant()), out var comp) && extrasP.TryGetValue((comp, K(key[(sep + 3)..])), out var p) ? p : "";
            else v = extrasT.TryGetValue((torreId, K(key)), out var t) ? t : "";
            return double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : (string.IsNullOrWhiteSpace(v) ? null : v);
        }

        var nativas = new List<ColumnaNativa>
        {
            new("Torre", "Torre", TipoCampoTabla.Texto, id => porId.TryGetValue(id, out var t) ? t.Codigo : null),
            new("Latitud", "Latitud", TipoCampoTabla.Numero, id => porId.TryGetValue(id, out var t) ? t.Latitud : null),
            new("Longitud", "Longitud", TipoCampoTabla.Numero, id => porId.TryGetValue(id, out var t) ? t.Longitud : null),
        };
        foreach (var cx in clavesX.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var clave = cx;
            nativas.Add(new ColumnaNativa(clave, clave[2..], TipoCampoTabla.Texto, id => ValorX(id, clave), true, "Excel"));
        }
        var motor = await MotorTabla.CargarLecturaAsync(db, "torres", nativas, torres.Select(t => (t.Id, t.Codigo)));

        // Archivos del elemento
        var idsC = comps.Select(c => c.Id).ToList();
        var archComp = await db.ArchivosComponente.AsNoTracking().Where(a => !a.Eliminado && idsC.Contains(a.ComponenteId)).ToListAsync(ct);
        // Documentación de los parámetros de actividad
        var actividades = await db.Actividades.AsNoTracking().Where(a => !a.Eliminado).ToDictionaryAsync(a => a.Id, a => a.Nombre, ct);
        var parametros = await db.ParametrosActividad.AsNoTracking().Where(p => !p.Eliminado).ToDictionaryAsync(p => p.Id, p => p.ActividadId, ct);
        var registros = await db.RegistrosParametro.AsNoTracking().Where(r => idsC.Contains(r.ComponenteId))
            .Select(r => new { r.Id, r.ComponenteId, r.ParametroActividadId }).ToDictionaryAsync(r => r.Id, ct);
        var idsR = registros.Keys.ToList();
        var archReg = await db.ArchivosRegistroParametro.AsNoTracking().Where(a => !a.Eliminado && idsR.Contains(a.RegistroParametroId)).ToListAsync(ct);

        var elementos = torres.ToDictionary(t => t.Id, t => new ElementoDossier { Id = t.Id, Grupo = t.TiroId is int ti && tiros.TryGetValue(ti, out var n) ? n : null });
        var clases = new List<string> { ClaseElemento };
        foreach (var a in archComp.OrderBy(a => a.Id))
        {
            if (!compPorId.TryGetValue(a.ComponenteId, out var c) || !elementos.TryGetValue(c.ElementoTrazadoId, out var el)) continue;
            el.Archivos.Add(new ArchivoDossier(ClaseElemento, a.NombreOriginal, a.RutaArchivo, Carpeta: c.Nombre));
        }
        foreach (var a in archReg.OrderBy(a => a.Id))
        {
            if (!registros.TryGetValue(a.RegistroParametroId, out var r) || !compPorId.TryGetValue(r.ComponenteId, out var c)
                || !parametros.TryGetValue(r.ParametroActividadId, out var actId) || !actividades.TryGetValue(actId, out var act)
                || !elementos.TryGetValue(c.ElementoTrazadoId, out var el)) continue;
            if (!clases.Contains(act)) clases.Add(act);
            el.Archivos.Add(new ArchivoDossier(act, a.NombreOriginal, a.RutaArchivo, Carpeta: $"{c.Nombre}/{act}"));
        }

        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = torres.Select(t => elementos[t.Id]).ToList(), ColumnaNombre = "Torre",
            NombreGrupo = "Tiro", Clases = clases
        };
    }

    private static (int, string) OrdenNatural(string n)
    {
        var d = new string(n.TakeWhile(char.IsDigit).ToArray());
        return (int.TryParse(d, out var v) ? v : int.MaxValue, n);
    }
}
