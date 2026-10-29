using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Riesgos;

// Convierte las celdas numeradas del Excel ("1. … \n2. …") en elementos
// sueltos, y mantiene los campos de texto del riesgo como copia sin números.
//
// Reglas, revisadas contra las 6 hojas del libro MATRIZ_RIESGOS_LT_500_kV_SNT:
//   · Un elemento por línea que empiece con número ("1.", "2)", "3-", o
//     "5  " con dos espacios, como aparece en V5) o con viñeta (-, *, •).
//   · "11.2020 …" NO es un número de lista (lo sigue un dígito): es fecha.
//   · Si la celda tiene numeración, una línea sin número se pega al
//     elemento anterior (es continuación). Si no tiene ninguna, cada línea
//     es un elemento (así viene la hoja V0).
//   · No se corta dentro de una línea: "…gestión del 2. Sistema Integrado"
//     es texto corrido, no un segundo elemento.
public static class ElementosRiesgo
{
    private static readonly Regex Num = new(@"^\s*\d{1,2}(?:\s*[.)\-](?!\d)|\s{2,})\s*(.*)$");
    private static readonly Regex Vineta = new(@"^\s*[-•*·▪]\s*(.*)$");

    public static readonly TipoElementoRiesgo[] Tipos =
    {
        TipoElementoRiesgo.Causa, TipoElementoRiesgo.Consecuencia, TipoElementoRiesgo.Disparador,
        TipoElementoRiesgo.PlanRespuesta, TipoElementoRiesgo.Seguimiento
    };

    public static string Nombre(TipoElementoRiesgo t) => t switch
    {
        TipoElementoRiesgo.Causa => "Causas",
        TipoElementoRiesgo.Consecuencia => "Consecuencias",
        TipoElementoRiesgo.Disparador => "Disparadores",
        TipoElementoRiesgo.PlanRespuesta => "Plan de respuesta",
        _ => "Cómo se hace el seguimiento"
    };

    public static List<string> Partir(string? texto)
    {
        var res = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) return res;
        var lineas = texto.Replace("\r", "").Split('\n')
            .Select(l => l.Trim()).Where(l => l.Trim('.', ' ').Length > 0).ToList();
        var hayMarcas = lineas.Any(l => Num.IsMatch(l) || Vineta.IsMatch(l));
        foreach (var l in lineas)
        {
            var m = Num.Match(l);
            if (!m.Success) m = Vineta.Match(l);
            if (m.Success)
            {
                var t = m.Groups[1].Value.Trim();
                if (t.Length > 0) res.Add(t);
            }
            else if (hayMarcas && res.Count > 0) res[^1] = res[^1] + " " + l;
            else res.Add(l);
        }
        return res.Select(x => Regex.Replace(x, @"\s{2,}", " ").Trim())
                  .Where(x => x.Trim('.', ' ').Length > 0).ToList();
    }

    public static string Texto(Riesgo r, TipoElementoRiesgo t) => t switch
    {
        TipoElementoRiesgo.Causa => r.Causa,
        TipoElementoRiesgo.Consecuencia => r.Consecuencia,
        TipoElementoRiesgo.Disparador => r.Disparadores,
        TipoElementoRiesgo.PlanRespuesta => r.PlanRespuesta,
        _ => r.ComoSeHaceSeguimiento
    };

    // Copia de texto (sin números) que se guarda en el propio riesgo.
    public static void Sincronizar(Riesgo r, IEnumerable<ElementoRiesgo> elementos)
    {
        string J(TipoElementoRiesgo t) => string.Join("\n", elementos.Where(e => e.Tipo == t).OrderBy(e => e.Orden).Select(e => e.Texto));
        r.Causa = J(TipoElementoRiesgo.Causa);
        r.Consecuencia = J(TipoElementoRiesgo.Consecuencia);
        r.Disparadores = J(TipoElementoRiesgo.Disparador);
        r.PlanRespuesta = J(TipoElementoRiesgo.PlanRespuesta);
        r.ComoSeHaceSeguimiento = J(TipoElementoRiesgo.Seguimiento);
    }

    // Elementos a partir de los textos del riesgo (importación y paso inicial).
    public static List<ElementoRiesgo> DesdeTextos(Riesgo r)
    {
        var l = new List<ElementoRiesgo>();
        foreach (var t in Tipos)
        {
            var o = 0;
            foreach (var x in Partir(Texto(r, t)))
                l.Add(new ElementoRiesgo { RiesgoId = r.Id, Tipo = t, Orden = ++o, Texto = x });
        }
        return l;
    }

    // Paso único: los riesgos que todavía no tienen elementos (creados antes
    // de esta función) se separan aquí. Incluye los que están en la Papelera,
    // para que al restaurarlos lleguen completos. Seguro de repetir.
    public static void ConvertirPendientes(AppDbContext db)
    {
        var conElementos = db.ElementosRiesgo.AsNoTracking().Select(e => e.RiesgoId).Distinct().ToHashSet();
        var pendientes = db.Riesgos.IgnoreQueryFilters().ToList().Where(r => !conElementos.Contains(r.Id)).ToList();
        if (pendientes.Count == 0) return;
        foreach (var r in pendientes)
        {
            var els = DesdeTextos(r);
            if (els.Count == 0) continue;
            db.ElementosRiesgo.AddRange(els);
            Sincronizar(r, els);
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
