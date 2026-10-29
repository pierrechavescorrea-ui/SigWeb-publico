using SigWeb.Models;

namespace SigWeb.Data.Trazado;

// Toda torre debe tener, obligatoriamente, un elemento especial "Hoja de
// vida" (no se puede renombrar ni eliminar, y siempre muestra todas las
// actividades). Este sembrador le crea uno a cualquier torre que todavía
// no lo tenga — las 147 ya cargadas antes de esta función, y cualquier
// torre nueva por si algo falla al crearla. Seguro de llamar en cada
// arranque: si ya todas la tienen, no hace nada.
public static class SembradorHojaDeVida
{
    public static void SembrarSiHaceFalta(AppDbContext db)
    {
        var torresConHojaDeVida = db.Componentes
            .Where(c => c.EsHojaDeVida)
            .Select(c => c.ElementoTrazadoId)
            .ToHashSet();

        var torresSinHojaDeVida = db.ElementosTrazado
            .Where(t => !torresConHojaDeVida.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();

        if (torresSinHojaDeVida.Count == 0) return;

        foreach (var torreId in torresSinHojaDeVida)
        {
            db.Componentes.Add(new Componente
            {
                ElementoTrazadoId = torreId,
                Nombre = "Hoja de vida",
                EsPredeterminado = true,
                EsHojaDeVida = true,
                Orden = -1 // siempre antes que las patas (Orden 0,1,2,3...)
            });
        }

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
