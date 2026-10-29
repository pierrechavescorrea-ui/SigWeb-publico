using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Modelos3D;

// Tipo, familia, cuerpo y modelo 3D de cada torre: la regla de Línea 3D, compartida con Avance de obra
// para que las dos vistas dibujen lo mismo. Sin modelo vinculado = geometría paramétrica por familia y cuerpo.
public record TipoTorre(string? Texto, string Familia, int Cuerpo, ConfigModelo? Modelo);

public static class TiposTorre
{
    // Regla anterior: toda torre del mismo tipo que la 102NNN usa el modelo "BE".
    public const string CodigoTorreModelada = "102NNN";
    private static readonly Regex Rx = new(@"^\s*([A-Za-z]+)\s*(\d+)?", RegexOptions.Compiled);

    // Columna de la tabla de torres con el tipo y cuerpo (ej. "BE4", "BP6").
    public static string? ClaveTipo(IEnumerable<string> claves)
    {
        var l = claves.ToList();
        return l.FirstOrDefault(c => c.Contains("cuerpo", StringComparison.OrdinalIgnoreCase))
            ?? l.FirstOrDefault(c => c.Contains("tipo", StringComparison.OrdinalIgnoreCase)
                && !c.Contains("cimenta", StringComparison.OrdinalIgnoreCase) && !c.Contains("suelo", StringComparison.OrdinalIgnoreCase));
    }

    // claveForzada: columna elegida a mano (Avance de obra › Ajustes); null = la regla de Línea 3D.
    public static Dictionary<int, TipoTorre> Cargar(AppDbContext db, List<ConfigModelo> cfg, string? claveForzada = null)
    {
        var elementos = db.ElementosTrazado.AsNoTracking().Where(e => !e.Eliminado).Select(e => new { e.Id, e.Codigo, e.Nombre }).ToList();
        var claves = db.CamposExtraTorre.AsNoTracking().Where(c => !c.Eliminado).Select(c => c.Clave).Distinct().ToList();
        var claveTipo = string.IsNullOrWhiteSpace(claveForzada) ? ClaveTipo(claves) : claveForzada;
        var tipos = claveTipo is null
            ? new Dictionary<int, string>()
            : db.CamposExtraTorre.AsNoTracking().Where(c => !c.Eliminado && c.Clave == claveTipo)
                .ToList().GroupBy(c => c.ElementoTrazadoId).ToDictionary(g => g.Key, g => g.First().Valor.Trim());

        var modelada = elementos.FirstOrDefault(e => string.Equals(e.Codigo, CodigoTorreModelada, StringComparison.OrdinalIgnoreCase)
                                                  || string.Equals(e.Nombre, CodigoTorreModelada, StringComparison.OrdinalIgnoreCase));
        string? tipoReal = null;
        if (modelada is not null) tipos.TryGetValue(modelada.Id, out tipoReal);
        var modeloBE = cfg.FirstOrDefault(c => c.Vinculado && ConfigModelos3D.Norm(c.Nombre) == "BE");

        var r = new Dictionary<int, TipoTorre>();
        foreach (var e in elementos)
        {
            string? texto = null, familia = ""; int cuerpo = 0;
            if (tipos.TryGetValue(e.Id, out var tt) && !string.IsNullOrWhiteSpace(tt))
            {
                texto = tt;
                var m = Rx.Match(tt);
                if (m.Success) { familia = m.Groups[1].Value.ToUpperInvariant(); cuerpo = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1; }
            }
            var c = ConfigModelos3D.ModeloPara(cfg, texto, familia, cuerpo);
            if (c is null && modeloBE is not null
                && ((tipoReal is not null && string.Equals(texto, tipoReal, StringComparison.OrdinalIgnoreCase)) || (modelada is not null && e.Id == modelada.Id)))
                c = modeloBE;
            r[e.Id] = new TipoTorre(texto, familia, cuerpo, c);
        }
        return r;
    }
}
