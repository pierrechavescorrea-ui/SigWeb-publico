using SigWeb.Models;

namespace SigWeb.Data.Trazado;

// Mantiene el catálogo "Localización" (usado por Materiales) sincronizado
// con las torres cargadas en el Trazado. Cada torre tiene, como máximo, un
// ítem de catálogo vinculado por Id (ElementoTrazadoId), nunca por texto.
// Así, renombrar una torre o "migrarla" (misma torre, nuevo nombre en un
// nuevo KMZ/Excel) actualiza el catálogo en vez de duplicar la entrada, y
// crear una torre nueva ("conservar ambas") crea también un ítem nuevo.
public static class SincronizadorCatalogoLocalizacion
{
    private const string TipoLocalizacion = "Localización";

    // Llamar después de crear, renombrar o migrar una torre: crea el ítem
    // de catálogo si no existe, o lo renombra/reactiva si ya existía.
    public static void Sincronizar(AppDbContext db, ElementoTrazado torre)
    {
        var nombre = NombreParaCatalogo(torre);
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var item = db.Catalogos.FirstOrDefault(c => c.ElementoTrazadoId == torre.Id);

        if (item is null)
        {
            // ¿Ya existe un ítem manual con exactamente este nombre? Si es
            // así, lo adoptamos en vez de crear un duplicado.
            item = db.Catalogos.FirstOrDefault(c =>
                c.Tipo == TipoLocalizacion &&
                c.ElementoTrazadoId == null &&
                c.Nombre.ToLower() == nombre.ToLower());
        }

        if (item is null)
        {
            item = new ItemCatalogo
            {
                Tipo = TipoLocalizacion,
                Nombre = nombre,
                Descripcion = "Generado automáticamente desde el Trazado",
                Activo = true,
                ElementoTrazadoId = torre.Id
            };
            db.Catalogos.Add(item);
        }
        else
        {
            // Evitar choque de nombre con otro ítem distinto del mismo catálogo.
            bool chocaConOtro = db.Catalogos.Any(c =>
                c.Tipo == TipoLocalizacion &&
                c.Id != item.Id &&
                c.Nombre.ToLower() == nombre.ToLower());

            if (!chocaConOtro)
            {
                item.Nombre = nombre;
            }
            item.ElementoTrazadoId = torre.Id;
            item.Activo = true;
        }

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Llamar cuando una torre se manda a la papelera (Eliminado = true):
    // desactiva su ítem de catálogo (no lo borra, por si se restaura o se
    // hereda luego desde un nuevo KMZ/Excel).
    public static void MarcarInactivo(AppDbContext db, int torreId)
    {
        var item = db.Catalogos.FirstOrDefault(c => c.ElementoTrazadoId == torreId);
        if (item is null) return;

        item.Activo = false;
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Backfill: recorre todas las torres activas y crea/actualiza sus
    // ítems de catálogo. Seguro de llamar en cada arranque.
    public static void SincronizarTodas(AppDbContext db)
    {
        var torres = db.ElementosTrazado.Where(e => !e.Eliminado).ToList();
        foreach (var torre in torres)
        {
            Sincronizar(db, torre);
        }
    }

    private static string NombreParaCatalogo(ElementoTrazado torre)
        => !string.IsNullOrWhiteSpace(torre.Nombre) ? torre.Nombre! : torre.Codigo;
}
