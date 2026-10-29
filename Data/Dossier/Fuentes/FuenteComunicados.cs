using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Comunicados;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Comunicados: mismas columnas y cálculos que la página (LogicaComunicados); adjuntos Archivo y Nube
// (los enlaces van al ZIP como accesos directos .url). Grupo = Entrada / Salida.
public sealed class FuenteComunicados : IFuenteDossier
{
    public string Clave => "comunicados";
    public string Nombre => "Comunicados";
    public string Icono => "comunicados";
    private static readonly LogicaComunicados.Ajustes AjustesExcel = new(8, "INGETEC", "JEJ");

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var ajustes = AjustesExcel;
        var cfg = await db.Configuracion.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (cfg is not null) ajustes = new(cfg.ComunicadosPlazoDias ?? AjustesExcel.PlazoDias,
            string.IsNullOrWhiteSpace(cfg.ComunicadosRespondeSalida) ? AjustesExcel.RespondeSalida : cfg.ComunicadosRespondeSalida,
            string.IsNullOrWhiteSpace(cfg.ComunicadosRespondeEntrada) ? AjustesExcel.RespondeEntrada : cfg.ComunicadosRespondeEntrada);
        var comunicados = await db.Comunicados.AsNoTracking().Where(c => !c.Eliminado).OrderBy(c => c.Fecha).ThenBy(c => c.Id).ToListAsync(ct);
        var porId = comunicados.ToDictionary(c => c.Id);
        var ids = comunicados.Select(c => c.Id).ToList();
        var adjuntos = (await db.AdjuntosComunicado.AsNoTracking().Where(a => !a.Eliminado && ids.Contains(a.ComunicadoId)).ToListAsync(ct))
            .GroupBy(a => a.ComunicadoId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Clase).ThenByDescending(a => a.FechaCarga).ToList());
        var cargas = await db.CargasComunicados.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.NombreArchivo + (string.IsNullOrWhiteSpace(c.Hoja) ? "" : " › " + c.Hoja), ct);
        var calc = LogicaComunicados.Calcular(comunicados, ajustes, DateTime.Today);

        Comunicado? C(int id) => porId.GetValueOrDefault(id);
        LogicaComunicados.Calculo K(int id) => calc.TryGetValue(id, out var c) ? c : new();
        var nativas = new[]
        {
            new ColumnaNativa("Tipo", "Tipo", TipoCampoTabla.Texto, id => C(id)?.Tipo),
            new ColumnaNativa("Remitente", "Remitente", TipoCampoTabla.Texto, id => C(id)?.Remitente),
            new ColumnaNativa("Sentido", "Entrada / Salida", TipoCampoTabla.Texto, id => C(id)?.Sentido),
            new ColumnaNativa("Consecutivo", "Consecutivo", TipoCampoTabla.Texto, id => C(id)?.Consecutivo),
            new ColumnaNativa("Fecha", "Fecha", TipoCampoTabla.Fecha, id => C(id)?.Fecha),
            new ColumnaNativa("Descripcion", "Descripción", TipoCampoTabla.Texto, id => C(id)?.Descripcion),
            new ColumnaNativa("Requiere", "Requiere respuesta", TipoCampoTabla.Texto, id => C(id)?.RequiereRespuesta switch { true => "Sí", false => "No", _ => null }),
            new ColumnaNativa("Estado", "Estado", TipoCampoTabla.Texto, id => K(id).Estado),
            new ColumnaNativa("ConsecutivoRta", "Consecutivo RTA", TipoCampoTabla.Texto, id => C(id)?.ConsecutivoRespuesta),
            new ColumnaNativa("Sugerida", "Respuesta sugerida", TipoCampoTabla.Texto, id => K(id).Sugerida?.Consecutivo),
            new ColumnaNativa("FechaRta", "Fecha respuesta", TipoCampoTabla.Fecha, id => K(id).FechaRespuesta),
            new ColumnaNativa("Dias", "Tiempo transcurrido (días)", TipoCampoTabla.Numero, id => K(id).Dias),
            new ColumnaNativa("Plazo", "Plazo", TipoCampoTabla.Texto, id => K(id).Plazo),
            new ColumnaNativa("ResponsableRta", "Responsable de RTA", TipoCampoTabla.Texto, id => C(id)?.ResponsableRespuesta),
            new ColumnaNativa("Observaciones", "Observaciones", TipoCampoTabla.Texto, id => C(id)?.Observaciones),
            new ColumnaNativa("Responsable", "Responsable", TipoCampoTabla.Texto, id => K(id).Responsable),
            new ColumnaNativa("Adjuntos", "Adjuntos", TipoCampoTabla.Numero, id => (double)(adjuntos.TryGetValue(id, out var l) ? l.Count : 0)),
            new ColumnaNativa("Carga", "Carga", TipoCampoTabla.Texto, id => C(id)?.CargaComunicadosId is int k && cargas.TryGetValue(k, out var n) ? n : null, visiblePorDefecto: false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, LogicaComunicados.Tabla, nativas, comunicados.Select(c => (c.Id, c.Consecutivo)));

        var elementos = comunicados.Select(c =>
        {
            var el = new ElementoDossier { Id = c.Id, Grupo = c.Sentido };
            if (adjuntos.TryGetValue(c.Id, out var l))
                foreach (var a in l)
                    el.Archivos.Add(a.Clase == AdjuntoComunicado.Nube
                        ? new ArchivoDossier("Nube", string.IsNullOrWhiteSpace(a.Nombre) ? "Enlace" : a.Nombre, a.Ruta, Enlace: true)
                        : new ArchivoDossier("Archivos", a.Nombre, a.Ruta));
            return el;
        }).ToList();

        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Consecutivo", NombreGrupo = "Sentido",
            Clases = new() { "Archivos", "Nube" }
        };
    }
}
