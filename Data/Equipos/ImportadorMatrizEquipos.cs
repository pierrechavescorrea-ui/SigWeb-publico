using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Equipos;

public class ResultadoImportacionEquipos
{
    public int EquiposNuevos { get; set; }
    public int CalibracionesNuevas { get; set; }
    public int FilasOmitidas { get; set; }
}

// Importa la "Matriz de equipos de medición" (F03-PROCES-02): cada hoja trae
// una fila por calibración; las filas del mismo instrumento + marca + modelo
// + serie forman un solo Equipo con varias calibraciones en su historial.
// Segura de repetir: no duplica equipos ni calibraciones ya cargadas.
public static class ImportadorMatrizEquipos
{
    private static string Norm(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "";
        var d = t.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(c));
        return string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Texto(IXLCell c)
    {
        XLCellValue v = c.HasFormula ? c.CachedValue : c.Value;
        return v.IsBlank ? "" : (v.IsDateTime ? v.GetDateTime().ToString("yyyy-MM-dd") : v.ToString().Trim());
    }

    private static DateTime? Fecha(IXLCell c)
    {
        XLCellValue v = c.HasFormula ? c.CachedValue : c.Value;
        DateTime? d = null;
        if (v.IsDateTime) d = v.GetDateTime();
        else if (v.IsText && DateTime.TryParse(v.GetText(), out var p)) d = p;
        return d is DateTime x && x.Year >= 2000 ? x.Date : null;   // 1900-12-31 es el "#N/A" de Excel
    }

    public static async Task<ResultadoImportacionEquipos> ImportarAsync(AppDbContext db, Stream archivo, string nombreArchivo = "Excel")
    {
        var res = new ResultadoImportacionEquipos();
        var carga = new CargaEquipos { NombreArchivo = nombreArchivo };
        db.CargasEquipos.Add(carga);
        await db.SaveChangesAsync();
        SembradorCamposEquipo.SembrarSiHaceFalta(db);
        var campos = await db.CamposEquipo.AsNoTracking().ToListAsync();
        int? Campo(string clave) => campos.FirstOrDefault(c => c.Clave == clave)?.Id;

        // Equipos ya existentes, por clave instrumento|marca|modelo|serie
        var existentes = new Dictionary<string, int>();
        var equiposDb = await db.Equipos.AsNoTracking().Where(e => !e.Eliminado).ToListAsync();
        var valoresDb = await db.ValoresCampoEquipo.AsNoTracking().ToListAsync();
        string Val(int eqId, string clave) => campos.FirstOrDefault(c => c.Clave == clave) is { } c
            ? valoresDb.FirstOrDefault(v => v.EquipoId == eqId && v.CampoEquipoId == c.Id)?.Valor ?? "" : "";
        var sinSerie = new Dictionary<string, int>();      // n-ésimo equipo sin serie de cada instrumento+marca+modelo
        foreach (var e in equiposDb.OrderBy(x => x.Id))
        {
            var serie = Val(e.Id, "NoSerie");
            var baseK = $"{Norm(e.Nombre)}|{Norm(Val(e.Id, "Marca"))}|{Norm(Val(e.Id, "Modelo"))}";
            if (serie != "") existentes[$"{baseK}|{Norm(serie)}"] = e.Id;
            else { sinSerie[baseK] = sinSerie.GetValueOrDefault(baseK) + 1; existentes[$"{baseK}|#{sinSerie[baseK]}"] = e.Id; }
        }
        var vistosSinSerie = new Dictionary<string, int>();

        using var wb = new XLWorkbook(archivo);
        foreach (var ws in wb.Worksheets)
        {
            // fila de encabezados: la que tiene "INSTRUMENTO"
            IXLRow? enc = null;
            foreach (var r in ws.Rows(1, 20))
                if (r.CellsUsed().Any(c => Norm(Texto(c)) == "INSTRUMENTO")) { enc = r; break; }
            if (enc is null) continue;

            var col = new Dictionary<string, int>();
            foreach (var c in enc.CellsUsed())
            {
                var h = Norm(Texto(c));
                if (h == "INSTRUMENTO") col["inst"] = c.Address.ColumnNumber;
                else if (h == "LABORATORIO") col["lab"] = c.Address.ColumnNumber;
                else if (h == "MARCA") col["marca"] = c.Address.ColumnNumber;
                else if (h == "MODELO") col["modelo"] = c.Address.ColumnNumber;
                else if (h.StartsWith("NO. DE SERIE")) col["serie"] = c.Address.ColumnNumber;
                else if (h.StartsWith("FECHA DE CALIBRACION")) col["fecha"] = c.Address.ColumnNumber;
                else if (h.StartsWith("PROXIMA FECHA")) col["prox"] = c.Address.ColumnNumber;
                else if (h.StartsWith("CERTIFICADO")) col["cert"] = c.Address.ColumnNumber;
                else if (h.StartsWith("UBICACION")) col["ubic"] = c.Address.ColumnNumber;
                else if (h.StartsWith("PERIODICIDAD")) col["per"] = c.Address.ColumnNumber;
                else if (h == "SERVICIO") col["serv"] = c.Address.ColumnNumber;
            }
            if (!col.ContainsKey("inst")) continue;
            string T(IXLRow r, string k) => col.TryGetValue(k, out var n) ? Texto(r.Cell(n)) : "";

            foreach (var fila in ws.Rows(enc.RowNumber() + 1, ws.LastRowUsed()?.RowNumber() ?? enc.RowNumber()))
            {
                var nombre = T(fila, "inst");
                if (nombre == "") { if (fila.CellsUsed().Any()) res.FilasOmitidas++; continue; }

                var marca = T(fila, "marca"); var modelo = T(fila, "modelo"); var serie = T(fila, "serie");
                var baseKey = $"{Norm(nombre)}|{Norm(marca)}|{Norm(modelo)}";
                string clave;
                if (serie != "") clave = $"{baseKey}|{Norm(serie)}";
                else { vistosSinSerie[baseKey] = vistosSinSerie.GetValueOrDefault(baseKey) + 1; clave = $"{baseKey}|#{vistosSinSerie[baseKey]}"; }

                var fecha = col.ContainsKey("fecha") ? Fecha(fila.Cell(col["fecha"])) : null;
                var prox = col.ContainsKey("prox") ? Fecha(fila.Cell(col["prox"])) : null;
                int? per = int.TryParse(T(fila, "per"), out var pm) && pm > 0 ? pm : null;
                if (per is null && fecha is DateTime f0 && prox is DateTime p0)
                {
                    var m = (p0.Year - f0.Year) * 12 + p0.Month - f0.Month;
                    if (m > 0) per = m;
                }

                if (!existentes.TryGetValue(clave, out var equipoId))
                {
                    var eq = new Equipo { Nombre = nombre, PeriodicidadMeses = per, CargaEquiposId = carga.Id };
                    db.Equipos.Add(eq);
                    await db.SaveChangesAsync();
                    equipoId = eq.Id;
                    existentes[clave] = equipoId;
                    res.EquiposNuevos++;
                }
                else if (per is not null)
                {
                    var eq = await db.Equipos.FirstAsync(e => e.Id == equipoId);
                    eq.PeriodicidadMeses = per;
                }

                // campos de la ficha (las hojas más nuevas sobrescriben ubicación / servicio)
                async Task Poner(string cl, string valor)
                {
                    if (valor == "" || Campo(cl) is not int cid) return;
                    var v = await db.ValoresCampoEquipo.FirstOrDefaultAsync(x => x.EquipoId == equipoId && x.CampoEquipoId == cid);
                    if (v is null) db.ValoresCampoEquipo.Add(new ValorCampoEquipo { EquipoId = equipoId, CampoEquipoId = cid, Valor = valor });
                    else v.Valor = valor;
                }
                await Poner("Marca", marca); await Poner("Modelo", modelo); await Poner("NoSerie", serie);
                await Poner("Ubicacion", T(fila, "ubic"));
                var serv = T(fila, "serv");
                if (serv != "") await Poner("Servicio", serv.Equals("SI", StringComparison.OrdinalIgnoreCase) || serv == "Sí" ? "Sí" : "No");

                // calibración de esa fila
                if (fecha is DateTime fc)
                {
                    var cert = T(fila, "cert");
                    var ya = await db.IntervencionesEquipo.AnyAsync(i => i.EquipoId == equipoId && i.Tipo == TipoIntervencionEquipo.Calibracion
                                                                         && i.Fecha == fc && (i.NumeroCertificado ?? "") == cert);
                    if (!ya)
                    {
                        db.IntervencionesEquipo.Add(new IntervencionEquipo
                        {
                            EquipoId = equipoId, CargaEquiposId = carga.Id, Tipo = TipoIntervencionEquipo.Calibracion, Fecha = fc,
                            ProximaFecha = prox ?? EstadoEquipo.ProponerProxima(fc, per),
                            Responsable = T(fila, "lab") is { Length: > 0 } l ? l : null,
                            NumeroCertificado = cert == "" ? null : cert,
                        });
                        res.CalibracionesNuevas++;
                    }
                }
                await db.SaveChangesAsync();
            }
        }
        if (res.EquiposNuevos == 0 && res.CalibracionesNuevas == 0)
        {
            var vacia = await db.CargasEquipos.FirstOrDefaultAsync(c => c.Id == carga.Id);
            if (vacia is not null) { db.CargasEquipos.Remove(vacia); await db.SaveChangesAsync(); }
        }
        db.ChangeTracker.Clear();
        return res;
    }

    // ---------- Importación con mapeo de columnas (PanelImportarExcel) ----------
    // Destinos: eq:Nombre (obligatorio), eq:Periodicidad, cal:Fecha, cal:Proxima, cal:Certificado,
    // cal:Laboratorio, ce:{Clave de CampoEquipo}; lo demás (c:{id} / nueva) lo guarda la página como
    // Campos de la tabla con FilasEquipo. Misma identidad que la importación clásica: instrumento +
    // marca + modelo + serie; no duplica equipos ni calibraciones ya cargadas.
    public static async Task<(ResultadoImportacionEquipos Res, int? CargaId, List<(int EquipoId, string[] Fila)> Filas)> ImportarAsync(
        AppDbContext db, SigWeb.Data.Documentos.ImportacionExcel imp)
    {
        var res = new ResultadoImportacionEquipos();
        var filasEq = new List<(int, string[])>();
        var colNombre = imp.ColumnaDe("eq:Nombre");
        if (colNombre is null) return (res, null, filasEq);
        string T(string[] f, string destino) => imp.ColumnaDe(destino) is int c ? SigWeb.Data.Documentos.ImportacionExcel.Celda(f, c).Trim() : "";
        DateTime? F(string[] f, string destino) => SigWeb.Data.Documentos.FechaTexto.Parsear(T(f, destino)) is DateTime d && d.Year >= 2000 ? d.Date : null;

        var carga = new CargaEquipos { NombreArchivo = imp.NombreArchivo, Hoja = imp.Hoja, FilaEncabezados = imp.FilaEncabezados };
        db.CargasEquipos.Add(carga);
        await db.SaveChangesAsync();
        SembradorCamposEquipo.SembrarSiHaceFalta(db);
        var campos = await db.CamposEquipo.AsNoTracking().ToListAsync();
        int? Campo(string clave) => campos.FirstOrDefault(c => c.Clave == clave)?.Id;

        var existentes = new Dictionary<string, int>();
        var equiposDb = await db.Equipos.AsNoTracking().Where(e => !e.Eliminado).ToListAsync();
        var valoresDb = await db.ValoresCampoEquipo.AsNoTracking().ToListAsync();
        string Val(int eqId, string clave) => campos.FirstOrDefault(c => c.Clave == clave) is { } c
            ? valoresDb.FirstOrDefault(v => v.EquipoId == eqId && v.CampoEquipoId == c.Id)?.Valor ?? "" : "";
        var sinSerie = new Dictionary<string, int>();
        foreach (var e in equiposDb.OrderBy(x => x.Id))
        {
            var serie = Val(e.Id, "NoSerie");
            var baseK = $"{Norm(e.Nombre)}|{Norm(Val(e.Id, "Marca"))}|{Norm(Val(e.Id, "Modelo"))}";
            if (serie != "") existentes[$"{baseK}|{Norm(serie)}"] = e.Id;
            else { sinSerie[baseK] = sinSerie.GetValueOrDefault(baseK) + 1; existentes[$"{baseK}|#{sinSerie[baseK]}"] = e.Id; }
        }
        var vistosSinSerie = new Dictionary<string, int>();
        var camposDestino = imp.Destinos.Values.Where(d => d.StartsWith("ce:")).Select(d => d[3..]).Distinct().ToList();

        foreach (var fila in imp.Filas)
        {
            var nombre = SigWeb.Data.Documentos.ImportacionExcel.Celda(fila, colNombre.Value).Trim();
            if (nombre == "") { res.FilasOmitidas++; continue; }
            var marca = T(fila, "ce:Marca"); var modelo = T(fila, "ce:Modelo"); var serie = T(fila, "ce:NoSerie");
            var baseKey = $"{Norm(nombre)}|{Norm(marca)}|{Norm(modelo)}";
            string clave;
            if (serie != "") clave = $"{baseKey}|{Norm(serie)}";
            else { vistosSinSerie[baseKey] = vistosSinSerie.GetValueOrDefault(baseKey) + 1; clave = $"{baseKey}|#{vistosSinSerie[baseKey]}"; }

            var fecha = F(fila, "cal:Fecha");
            var prox = F(fila, "cal:Proxima");
            int? per = int.TryParse(new string(T(fila, "eq:Periodicidad").TakeWhile(char.IsDigit).ToArray()), out var pm) && pm > 0 ? pm : null;
            if (per is null && fecha is DateTime f0 && prox is DateTime p0)
            {
                var m = (p0.Year - f0.Year) * 12 + p0.Month - f0.Month;
                if (m > 0) per = m;
            }

            if (!existentes.TryGetValue(clave, out var equipoId))
            {
                var eq = new Equipo { Nombre = nombre, PeriodicidadMeses = per, CargaEquiposId = carga.Id };
                db.Equipos.Add(eq);
                await db.SaveChangesAsync();
                equipoId = eq.Id;
                existentes[clave] = equipoId;
                res.EquiposNuevos++;
            }
            else if (per is not null)
            {
                var eq = await db.Equipos.FirstAsync(e => e.Id == equipoId);
                eq.PeriodicidadMeses = per;
            }

            foreach (var cl in camposDestino)
            {
                var valor = T(fila, "ce:" + cl);
                if (valor == "" || Campo(cl) is not int cid) continue;
                if (cl == "Servicio") valor = valor.Equals("SI", StringComparison.OrdinalIgnoreCase) || valor == "Sí" ? "Sí" : valor.Equals("NO", StringComparison.OrdinalIgnoreCase) ? "No" : valor;
                var v = await db.ValoresCampoEquipo.FirstOrDefaultAsync(x => x.EquipoId == equipoId && x.CampoEquipoId == cid);
                if (v is null) db.ValoresCampoEquipo.Add(new ValorCampoEquipo { EquipoId = equipoId, CampoEquipoId = cid, Valor = valor });
                else v.Valor = valor;
            }

            if (fecha is DateTime fc)
            {
                var cert = T(fila, "cal:Certificado");
                var ya = await db.IntervencionesEquipo.AnyAsync(i => i.EquipoId == equipoId && i.Tipo == TipoIntervencionEquipo.Calibracion
                                                                     && i.Fecha == fc && (i.NumeroCertificado ?? "") == cert);
                if (!ya)
                {
                    db.IntervencionesEquipo.Add(new IntervencionEquipo
                    {
                        EquipoId = equipoId, CargaEquiposId = carga.Id, Tipo = TipoIntervencionEquipo.Calibracion, Fecha = fc,
                        ProximaFecha = prox ?? EstadoEquipo.ProponerProxima(fc, per),
                        Responsable = T(fila, "cal:Laboratorio") is { Length: > 0 } l ? l : null,
                        NumeroCertificado = cert == "" ? null : cert,
                    });
                    res.CalibracionesNuevas++;
                }
            }
            await db.SaveChangesAsync();
            filasEq.Add((equipoId, fila));
        }
        int? id = carga.Id;
        if (res.EquiposNuevos == 0 && res.CalibracionesNuevas == 0 && filasEq.Count == 0)
        {
            var vacia = await db.CargasEquipos.FirstOrDefaultAsync(c => c.Id == carga.Id);
            if (vacia is not null) { db.CargasEquipos.Remove(vacia); await db.SaveChangesAsync(); }
            id = null;
        }
        db.ChangeTracker.Clear();
        return (res, id, filasEq);
    }
}
