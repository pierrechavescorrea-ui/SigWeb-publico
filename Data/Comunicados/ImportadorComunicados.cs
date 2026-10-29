using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Documentos;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Comunicados;

// Importa el Excel del listado de comunicados. Cada columna va a una columna propia, a un Campo
// existente, a uno nuevo o se omite (las que el Excel calcula con fórmulas: Estado, AUTO, Fecha 2,
// Tiempo transcurrido, Plazo, Responsable — la app las calcula sola).
// Un comunicado ya existente (mismo consecutivo y misma fecha) se actualiza; una celda vacía del
// Excel no borra lo que ya tenga la app (p. ej. una respuesta puesta a mano).
public static class ImportadorComunicados
{
    public const string Omitir = "omitir";

    public static IReadOnlyList<DestinoColumna> DestinosPropios => new List<DestinoColumna>
    {
        new("nat:Consecutivo", "Consecutivo", "consecutivo", "radicado", "numero", "no.", "codigo", "código"),
        new("nat:Fecha", "Fecha", "fecha", "fecha de radicado", "fecha comunicado"),
        new("nat:Tipo", "Tipo", "tipo", "tipo de comunicado"),
        new("nat:Remitente", "Remitente", "remitente", "de", "emisor"),
        new("nat:Sentido", "Entrada / Salida", "entrada / salida", "entrada/salida", "sentido", "e/s"),
        new("nat:Descripcion", "Descripción", "descripcion", "descripción", "asunto"),
        new("nat:Requiere", "Requiere respuesta", "requiere respuesta", "requiere rta", "requiere"),
        new("nat:ConsecutivoRta", "Consecutivo RTA", "consecutivo rta", "consecutivo respuesta", "respuesta", "rta"),
        new("nat:FechaRta", "Fecha respuesta", "fecha respuesta", "fecha rta", "fecha de respuesta"),
        new("nat:ResponsableRta", "Responsable de RTA", "responsable de rta", "area responsable", "área responsable"),
        new("nat:Observaciones", "Observaciones", "observaciones", "observacion", "notas"),
        new(Omitir, "(No importar · la app lo calcula)", "estado", "auto", "fecha 2", "tiempo transcurrido", "plazo", "responsable"),
    };

    public sealed record Resultado(int Creados, int Actualizados, int Omitidas, List<string> Avisos);

    public static async Task<Resultado> ImportarAsync(AppDbContext db, ImportacionExcel imp, Dictionary<int, string> nombresCampos)
    {
        int? Col(string d) => imp.ColumnaDe(d);
        string Val(string[] f, string d) => Col(d) is int c ? ImportacionExcel.Celda(f, c).Trim() : "";
        var avisos = new List<string>();
        if (Col("nat:Consecutivo") is null) throw new InvalidOperationException("Elige qué columna del Excel es el Consecutivo.");

        var carga = new CargaComunicados { NombreArchivo = imp.NombreArchivo, Hoja = imp.Hoja, FilaEncabezados = imp.FilaEncabezados, Fecha = DateTime.Now };
        db.CargasComunicados.Add(carga);
        await db.SaveChangesAsync();

        var existentes = await db.Comunicados.ToListAsync();
        var indice = existentes.GroupBy(c => (LogicaComunicados.Clave(c.Consecutivo), c.Fecha?.Date))
            .ToDictionary(g => g.Key, g => g.First());

        var usados = new HashSet<int>();
        var filasDe = new List<(Comunicado C, string[] F)>();
        int creados = 0, actualizados = 0, omitidas = 0;
        foreach (var f in imp.Filas)
        {
            var codigo = Val(f, "nat:Consecutivo");
            if (codigo.Length == 0) { omitidas++; continue; }
            var fecha = FechaTexto.Parsear(Val(f, "nat:Fecha"));
            var k = (LogicaComunicados.Clave(codigo), fecha?.Date);
            // Solo se emparejan con los que ya estaban (una vez cada uno): filas repetidas del mismo Excel
            // quedan como comunicados distintos, para que el listado las marque como código repetido.
            if (indice.TryGetValue(k, out var c) && usados.Add(c.Id)) actualizados++;
            else
            {
                c = new Comunicado { Consecutivo = codigo, Fecha = fecha, CargaComunicadosId = carga.Id };
                db.Comunicados.Add(c);
                creados++;
            }

            void Texto(string destino, Action<string> poner) { var v = Val(f, destino); if (v.Length > 0) poner(v); }
            Texto("nat:Tipo", v => c.Tipo = v);
            Texto("nat:Remitente", v => c.Remitente = v);
            Texto("nat:Sentido", v => c.Sentido = LogicaComunicados.Sentido(v));
            Texto("nat:Descripcion", v => c.Descripcion = v);
            Texto("nat:Requiere", v => c.RequiereRespuesta = LogicaComunicados.SiNo(v));
            Texto("nat:ConsecutivoRta", v => c.ConsecutivoRespuesta = v);
            Texto("nat:FechaRta", v => c.FechaRespuesta = FechaTexto.Parsear(v) ?? c.FechaRespuesta);
            Texto("nat:ResponsableRta", v => c.ResponsableRespuesta = v);
            Texto("nat:Observaciones", v => c.Observaciones = v);
            c.Consecutivo = codigo;
            filasDe.Add((c, f));
        }
        carga.FilasCreadas = creados; carga.FilasActualizadas = actualizados;
        await db.SaveChangesAsync();

        // Columnas a Campos: la existente elegida (su nombre) o una nueva con el encabezado del Excel.
        var aCampos = imp.Destinos.Where(kv => kv.Value == ImportacionExcel.Nueva || kv.Value.StartsWith("c:")).ToList();
        if (aCampos.Count > 0)
        {
            var nombres = aCampos.ToDictionary(kv => kv.Key, kv =>
                kv.Value.StartsWith("c:") && nombresCampos.TryGetValue(int.Parse(kv.Value[2..]), out var n) ? n : imp.Encabezados[kv.Key]);
            var extras = new Dictionary<int, Dictionary<string, string>>();
            foreach (var (c, f) in filasDe)
            {
                var d = extras.TryGetValue(c.Id, out var e) ? e : extras[c.Id] = new(StringComparer.OrdinalIgnoreCase);
                foreach (var (col, nombre) in nombres)
                {
                    var v = ImportacionExcel.Celda(f, col);
                    if (!string.IsNullOrWhiteSpace(v)) d[nombre] = v;   // vacío no borra lo que ya tenga
                }
            }
            db.ChangeTracker.Clear();
            await MotorTabla.GuardarExtrasExcelAsync(db, LogicaComunicados.Tabla, extras);
        }
        if (omitidas > 0) avisos.Add($"{omitidas} fila(s) sin consecutivo no se importaron.");
        return new Resultado(creados, actualizados, omitidas, avisos);
    }
}
