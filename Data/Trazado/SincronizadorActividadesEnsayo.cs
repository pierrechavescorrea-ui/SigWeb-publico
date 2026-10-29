using SigWeb.Models;

namespace SigWeb.Data.Trazado;

// Mantiene cada Tipo de Ensayo (módulo Ensayos) con su propia Actividad de
// ámbito Pata en Trazado, enlazada por Id (Actividad.TipoEnsayoId) en vez
// de por coincidencia de texto. Así, cualquier Tipo de Ensayo que el
// usuario cree (no solo los 2 sembrados de fábrica: "resistencia a la
// compresión" y "peso unitario o densidades") aparece automáticamente
// como una actividad disponible en la ficha de cada pata/elemento, y esa
// actividad se activa sola cuando el elemento ya tiene resultados de ese
// tipo — exactamente igual a como ya funcionaba para los 2 de fábrica,
// pero ahora relacional y para cualquier ensayo nuevo.
//
// El "activar/desactivar a mano" (ComponenteActividad) no se toca: sigue
// funcionando igual, por encima de este enlace.
public static class SincronizadorActividadesEnsayo
{
    private const string Prefijo = "Ensayos de ";

    // Llamar después de crear o renombrar un Tipo de Ensayo (ver
    // Ensayos.razor -> GuardarTipo): crea su Actividad si no existe, o la
    // adopta/renombra si ya existía (por el enlace o, para los activados
    // antes de este cambio, por coincidencia de nombre).
    public static void Sincronizar(AppDbContext db, TipoEnsayo tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo.Nombre)) return;

        var nombreActividad = Prefijo + tipo.Nombre.Trim();

        var actividad = db.Actividades.FirstOrDefault(a => a.TipoEnsayoId == tipo.Id);

        if (actividad is null)
        {
            // ¿Ya existe una actividad de ámbito Pata sin enlazar cuyo
            // nombre corresponda a este tipo (ej. las 2 sembradas de
            // fábrica, o una creada a mano antes de este cambio)? La
            // adoptamos en vez de crear una duplicada. Coincidencia
            // flexible (contiene, en cualquier dirección) porque el
            // nombre del Tipo de Ensayo no tiene por qué calcar al pie de
            // la letra el de la actividad (ej. Tipo "Densidades" <->
            // Actividad "Ensayos de peso unitario o densidades"). El
            // filtro por texto se hace en memoria (no es traducible a SQL).
            var candidatas = db.Actividades
                .Where(a => a.Ambito == AmbitoActividad.Pata && a.TipoEnsayoId == null && !a.Eliminado)
                .ToList();
            actividad = candidatas.FirstOrDefault(a => CoincideNombre(a.Nombre, tipo.Nombre));
        }

        if (actividad is null)
        {
            int orden = db.Actividades.Any(a => a.Ambito == AmbitoActividad.Pata)
                ? db.Actividades.Where(a => a.Ambito == AmbitoActividad.Pata).Max(a => a.Orden) + 1
                : 0;

            actividad = new Actividad
            {
                Nombre = nombreActividad,
                Ambito = AmbitoActividad.Pata,
                Activo = true,
                Orden = orden,
                TipoEnsayoId = tipo.Id
            };
            db.Actividades.Add(actividad);
        }
        else
        {
            actividad.Nombre = nombreActividad;
            actividad.TipoEnsayoId = tipo.Id;
        }

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Backfill: recorre todos los Tipos de Ensayo y asegura su actividad
    // enlazada. Segura de llamar en cada arranque. Al final, fusiona
    // cualquier actividad duplicada que haya quedado suelta (ver
    // FusionarDuplicadas) — por ejemplo, de instalaciones donde el enlace
    // automático no alcanzó a correr para todos los tipos antes de este
    // cambio.
    public static void SincronizarTodos(AppDbContext db)
    {
        var tipos = db.TiposEnsayo.ToList();
        foreach (var tipo in tipos)
        {
            Sincronizar(db, tipo);
        }

        FusionarDuplicadas(db);
    }

    // Limpieza: dos actividades de ámbito Pata pueden terminar refiriéndose
    // al mismo ensayo en la práctica (ej. "Ensayos de Densidades", ya
    // enlazada a su Tipo de Ensayo, y "Ensayos de peso unitario o
    // densidades", sembrada de fábrica y nunca adoptada) — se fusionan en
    // una sola: la enlazada se queda, y lo que el usuario ya hubiera
    // marcado manualmente (ComponenteActividad) en la duplicada se
    // reasigna a la enlazada antes de borrarla, para no perder nada.
    private static void FusionarDuplicadas(AppDbContext db)
    {
        var actividadesPata = db.Actividades.Where(a => a.Ambito == AmbitoActividad.Pata).ToList();
        var enlazadas = actividadesPata.Where(a => a.TipoEnsayoId != null).ToList();
        var sinEnlazar = actividadesPata.Where(a => a.TipoEnsayoId == null).ToList();
        if (enlazadas.Count == 0 || sinEnlazar.Count == 0) return;

        bool huboCambios = false;

        foreach (var canonica in enlazadas)
        {
            var duplicadas = sinEnlazar.Where(a => CoincideNombreActividad(a.Nombre, canonica.Nombre)).ToList();
            foreach (var duplicada in duplicadas)
            {
                var vinculos = db.ComponenteActividades.Where(ca => ca.ActividadId == duplicada.Id).ToList();
                foreach (var vinculo in vinculos)
                {
                    bool yaExiste = db.ComponenteActividades.Any(ca =>
                        ca.ComponenteId == vinculo.ComponenteId && ca.ActividadId == canonica.Id);
                    if (yaExiste) db.ComponenteActividades.Remove(vinculo);
                    else vinculo.ActividadId = canonica.Id;
                }

                db.Actividades.Remove(duplicada);
                sinEnlazar.Remove(duplicada);
                huboCambios = true;
            }
        }

        if (!huboCambios) return;

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    private static bool CoincideNombre(string nombreActividad, string nombreTipoEnsayo)
    {
        if (string.IsNullOrWhiteSpace(nombreActividad) || string.IsNullOrWhiteSpace(nombreTipoEnsayo)) return false;

        var sinPrefijo = QuitarPrefijo(nombreActividad);

        return sinPrefijo.Contains(nombreTipoEnsayo, StringComparison.OrdinalIgnoreCase)
            || nombreTipoEnsayo.Contains(sinPrefijo, StringComparison.OrdinalIgnoreCase);
    }

    // Igual que CoincideNombre, pero comparando dos nombres de Actividad
    // entre sí (ambos con o sin el prefijo "Ensayos de ") en vez de una
    // Actividad contra un Tipo de Ensayo.
    private static bool CoincideNombreActividad(string nombreA, string nombreB)
    {
        var a = QuitarPrefijo(nombreA);
        var b = QuitarPrefijo(nombreB);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;

        return a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string QuitarPrefijo(string nombre)
        => nombre.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase) ? nombre[Prefijo.Length..].Trim() : nombre.Trim();
}
