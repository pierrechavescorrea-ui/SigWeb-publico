using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Catalogos;

// Siembra los 6 catálogos que antes eran una lista fija en el código de
// Catalogos.razor (TiposCatalogo), ahora como filas de DefinicionCatalogo
// para que el usuario pueda agregar los suyos desde la pantalla. Seguro
// de llamar en cada arranque: si ya existe (por Nombre), no lo duplica.
public static class SembradorDefinicionesCatalogo
{
    public static void SembrarSiHaceFalta(AppDbContext db)
    {
        var yaExisten = db.DefinicionesCatalogo.IgnoreQueryFilters().Select(d => d.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);

        void Crear(string nombre, string entidadAsociada, int orden)
        {
            if (yaExisten.Contains(nombre)) return;
            db.DefinicionesCatalogo.Add(new DefinicionCatalogo
            {
                Nombre = nombre,
                EntidadAsociada = entidadAsociada,
                EsPredeterminado = true,
                Orden = orden
            });
        }

        Crear("Material", "Materiales — tipo de material", 0);
        Crear("Proveedor / Fabricante", "Materiales — proveedor o fabricante", 1);
        Crear("Unidad de medida", "Materiales y Ensayos — unidad de un campo o cantidad", 2);
        Crear("Criterio de aceptación", "Ensayos — criterio de aceptación de un resultado", 3);
        Crear("Documento de referencia", "Planos y Ensayos — documento externo citado", 4);
        Crear("Localización", "Trazado — se actualiza automáticamente desde las torres cargadas", 5);

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
