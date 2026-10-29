using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.ListadoMateriales;

// Torres, elementos (patas, etc.) y tiros a los que se puede ligar un material, con sus etiquetas.
// Se carga una vez (en el handler de carga de la página, nunca desde el render).
public sealed class LugaresMaterial
{
    public sealed record Torre(int Id, string Codigo, int? TiroId);
    public sealed record Elemento(int Id, int TorreId, string Nombre);
    public sealed record TiroL(int Id, string Nombre);

    public List<Torre> Torres { get; private set; } = new();
    public Dictionary<int, List<Elemento>> ElementosPorTorre { get; private set; } = new();
    public List<TiroL> Tiros { get; private set; } = new();
    private Dictionary<int, Torre> torrePorId = new();
    private Dictionary<int, Elemento> elementoPorId = new();
    private Dictionary<int, TiroL> tiroPorId = new();

    public static LugaresMaterial Cargar(AppDbContext db)
    {
        var l = new LugaresMaterial();
        l.Torres = db.ElementosTrazado.AsNoTracking().OrderBy(t => t.Codigo).Select(t => new Torre(t.Id, t.Codigo, t.TiroId)).ToList();
        // La "Hoja de vida" es la torre misma: no se ofrece como elemento.
        var elems = db.Componentes.AsNoTracking().Where(c => !c.EsHojaDeVida)
            .OrderBy(c => c.ElementoTrazadoId).ThenBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new Elemento(c.Id, c.ElementoTrazadoId, c.Nombre)).ToList();
        l.ElementosPorTorre = elems.GroupBy(e => e.TorreId).ToDictionary(g => g.Key, g => g.ToList());
        l.Tiros = db.Tiros.AsNoTracking().OrderBy(t => t.Orden).ThenBy(t => t.Nombre).Select(t => new TiroL(t.Id, t.Nombre)).ToList();
        l.torrePorId = l.Torres.ToDictionary(t => t.Id);
        l.elementoPorId = elems.ToDictionary(e => e.Id);
        l.tiroPorId = l.Tiros.ToDictionary(t => t.Id);
        return l;
    }

    public Torre? TorreDe(int? id) => id is int i && torrePorId.TryGetValue(i, out var t) ? t : null;
    public Torre? TorrePorCodigo(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : Torres.FirstOrDefault(t => string.Equals(t.Codigo.Trim(), codigo.Trim(), StringComparison.OrdinalIgnoreCase));
    public List<Elemento> ElementosDe(int? torreId) => torreId is int i && ElementosPorTorre.TryGetValue(i, out var l) ? l : new();

    // "T-12", "T-12 · Pata A", "Tiro 3".
    public string Etiqueta(UbicacionMaterial u)
    {
        if (u.TiroId is int ti) return tiroPorId.TryGetValue(ti, out var t) ? $"Tiro {t.Nombre}" : $"Tiro #{ti}";
        var torre = TorreDe(u.ElementoTrazadoId)?.Codigo ?? (u.ElementoTrazadoId is int x ? $"Torre #{x}" : "—");
        if (u.ComponenteId is int ci) return $"{torre} · {(elementoPorId.TryGetValue(ci, out var e) ? e.Nombre : "#" + ci)}";
        return torre;
    }
}
