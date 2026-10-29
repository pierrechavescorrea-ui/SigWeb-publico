using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Planos;

// Crea las disciplinas iniciales del listado maestro de planos y manda a
// "Estructural" todos los planos que aún no tienen grupo. Segura de repetir.
public static class SembradorGruposPlano
{
    public static void SembrarSiHaceFalta(AppDbContext db)
    {
        var iconos = db.IconosCatalogo.AsNoTracking().ToDictionary(i => i.Clave, i => i.Id);
        int? Ic(string k) => iconos.TryGetValue(k, out var id) ? id : null;

        var base_ = new[]
        {
            ("Estructural", "planos-estructural"),
            ("Civil", "planos-civil"),
            ("Eléctrico", "planos-electrico"),
        };
        int orden = 0;
        foreach (var (nombre, clave) in base_)
        {
            var g = db.GruposPlano.IgnoreQueryFilters().FirstOrDefault(x => x.Nombre == nombre);
            if (g is null)
                db.GruposPlano.Add(new GrupoPlano { Nombre = nombre, IconoCatalogoId = Ic(clave), Orden = orden });
            else if (g.IconoCatalogoId is null)
                g.IconoCatalogoId = Ic(clave);
            orden++;
        }
        db.SaveChanges();

        var estructural = db.GruposPlano.IgnoreQueryFilters().First(x => x.Nombre == "Estructural");
        var sinGrupo = db.PlanosMaestro.IgnoreQueryFilters().Where(p => p.GrupoPlanoId == null).ToList();
        foreach (var p in sinGrupo) p.GrupoPlanoId = estructural.Id;
        if (sinGrupo.Count > 0) db.SaveChanges();
    }
}
