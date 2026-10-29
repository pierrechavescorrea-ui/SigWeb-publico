using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Trazado;

// Las actividades de ámbito Pata son un catálogo nuevo y separado del de
// Torre. Le crea a cada instalación las dos típicas (Ensayos de
// resistencia a la compresión, Ensayos de peso unitario o densidades) la
// primera vez que corre; el usuario puede agregar más, renombrarlas o
// desactivarlas después desde el módulo Actividades (eligiendo "Pata").
// Seguro de llamar en cada arranque: si ya existen (por nombre), no hace nada.
public static class SembradorActividadesPata
{
    public static void SembrarSiHaceFalta(AppDbContext db)
    {
        var yaExisten = db.Actividades
            .Where(a => a.Ambito == AmbitoActividad.Pata)
            .Select(a => a.Nombre)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int orden = db.Actividades.Any()
            ? db.Actividades.Max(a => a.Orden) + 1
            : 0;

        void Crear(string nombre)
        {
            if (yaExisten.Contains(nombre)) return;
            db.Actividades.Add(new Actividad
            {
                Nombre = nombre,
                Orden = orden++,
                Ambito = AmbitoActividad.Pata
            });
        }

        Crear("Ensayos de resistencia a la compresión");
        Crear("Ensayos de peso unitario o densidades");

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
