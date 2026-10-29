using SigWeb.Models;

namespace SigWeb.Data.Trazado;

// Las torres cargadas antes de que existiera el concepto de "capas" no
// tienen CapaTrazadoId. Este sembrador las agrupa, una sola vez, en una
// capa llamada "Cargadas antes de esta función" para que aparezcan en el
// panel de Capas como cualquier otra (visible, apagable, eliminable). Es
// seguro llamarlo en cada arranque: si ya no queda ninguna torre huérfana,
// no hace nada.
public static class SembradorCapaInicial
{
    public static void SembrarSiHaceFalta(AppDbContext db)
    {
        var huerfanas = db.ElementosTrazado.Where(e => e.CapaTrazadoId == null).ToList();
        if (huerfanas.Count == 0) return;

        var capa = db.Capas.FirstOrDefault(c => c.Origen == "Inicial")
            ?? new CapaTrazado { Nombre = "Cargadas antes de esta función", Origen = "Inicial", FechaCarga = DateTime.Now };

        if (capa.Id == 0)
        {
            db.Capas.Add(capa);
            db.SaveChanges();
        }

        foreach (var e in huerfanas)
        {
            e.CapaTrazadoId = capa.Id;
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}