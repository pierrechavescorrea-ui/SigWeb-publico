using SigWeb.Models;

namespace SigWeb.Data.Equipos;

// Campos de fábrica de la ficha de un equipo (los de la matriz de Excel);
// después todo se configura desde el módulo. Solo corre si no hay ninguno.
public static class SembradorCamposEquipo
{
    public static void SembrarSiHaceFalta(AppDbContext db)
    {
        try
        {
            if (db.CamposEquipo.Any()) return;
            var orden = 0;
            foreach (var (clave, etiqueta) in new[] { ("Marca", "Marca"), ("Modelo", "Modelo"), ("NoSerie", "No. de serie"), ("Ubicacion", "Ubicación"), ("Servicio", "En servicio") })
                db.CamposEquipo.Add(new CampoEquipo { Clave = clave, Etiqueta = etiqueta, Orden = orden++ });
            db.SaveChanges();
        }
        catch (Exception) { /* la migración de Equipos aún no se aplicó: el módulo no está disponible hasta entonces */ }
    }
}
