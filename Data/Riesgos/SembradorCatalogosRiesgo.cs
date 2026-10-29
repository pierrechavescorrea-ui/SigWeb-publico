using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Riesgos;

// Crea en Catálogos las listas que la matriz de riesgos usa como desplegables.
// Los valores NO se inventan: salen de la hoja "Datos" del libro de la matriz
// al importarlo (CatalogosLibro). Lo que ya exista no se duplica ni se pisa,
// así que el usuario puede seguir agregando los suyos desde Catálogos.
public static class SembradorCatalogosRiesgo
{
    public const string EstadoActual = "Riesgo — estado actual";
    public const string Categoria = "Riesgo — categoría";
    public const string Estrategia = "Riesgo — estrategia de respuesta";
    public const string EstadoPlan = "Riesgo — estado del plan";
    public const string Area = "Riesgo — área";
    // Cargos que responden por el plan de respuesta (selección múltiple).
    public const string Cargo = "Riesgo — cargo responsable";
    // Lista vacía al crearla: los procesos los define la empresa en Catálogos.
    public const string ProcesoPlan = "Plan de acción — proceso";
    public const string OrigenHallazgo = "Plan de acción — origen del hallazgo";
    public const string TipoHallazgo = "Plan de acción — tipo de hallazgo";
    // Estados fijos de la app: el catálogo sirve para darles formato (color,
    // fondo, forma). Agregar ítems aquí no crea estados nuevos.
    public const string EstadoAccion = "Plan de acción — estado de la acción";
    public const string EstadoHallazgo = "Riesgo — estado del hallazgo";
    public const string ResultadoSeguimiento = "Riesgo — resultado del seguimiento";

    // Valores iniciales dados por el usuario (13/10/2026). Solo se cargan al
    // crear la definición: si luego se borran en Catálogos, no vuelven.
    private static readonly Dictionary<string, string[]> Iniciales = new()
    {
        [OrigenHallazgo] = new[] { "Auditoría", "Inspección", "Seguimiento", "Verificación cruzada" },
        [TipoHallazgo] = new[] { "No conformidad", "Oportunidad de mejora", "Observación" },
    };

    // Formato inicial de los estados fijos: los mismos colores que ya usaba la app.
    private static readonly Dictionary<string, (string Nombre, string Fondo, string Texto)[]> EstadosFijos = new()
    {
        [EstadoAccion] = new[] { ("Abierta", "#FF0000", "#ffffff"), ("En ejecución", "#FFC000", "#1f2a3a"), ("Cerrada", "#00B050", "#1f2a3a") },
        [EstadoHallazgo] = new[] { ("Abierto", "#dc3545", "#ffffff"), ("En tratamiento", "#fd7e14", "#ffffff"), ("Cerrado", "#198754", "#ffffff") },
        [ResultadoSeguimiento] = new[] { ("No materializado", "#198754", "#ffffff"), ("En observación", "#d9822b", "#ffffff"), ("Materializado", "#c0392b", "#ffffff") },
    };

    public static readonly (string Nombre, string Entidad)[] Definiciones =
    {
        (EstadoActual, "Riesgos — estado actual del riesgo"),
        (Categoria, "Riesgos — categoría del riesgo"),
        (Area, "Riesgos — área o disciplina afectada"),
        (Estrategia, "Riesgos — estrategia de respuesta"),
        (EstadoPlan, "Riesgos — estado del plan de respuesta"),
        (Cargo, "Riesgos — cargos responsables del plan de respuesta"),
        (ProcesoPlan, "Planes de acción — proceso responsable"),
        (OrigenHallazgo, "Planes de acción — de dónde sale el hallazgo"),
        (TipoHallazgo, "Planes de acción — clase de hallazgo"),
        (EstadoAccion, "Planes de acción — formato de los estados de cada acción"),
        (EstadoHallazgo, "Riesgos — formato de los estados de un hallazgo"),
        (ResultadoSeguimiento, "Riesgos — formato del resultado de un seguimiento"),
    };

    // Asegura que existan las 5 definiciones de catálogo (vacías si no hay
    // valores todavía). Seguro de llamar en cada arranque.
    public static void SembrarDefiniciones(AppDbContext db)
    {
        var ya = db.DefinicionesCatalogo.IgnoreQueryFilters().Select(d => d.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orden = 10;
        var creados = false;
        foreach (var (nombre, entidad) in Definiciones)
        {
            if (ya.Contains(nombre)) { orden++; continue; }
            db.DefinicionesCatalogo.Add(new DefinicionCatalogo { Nombre = nombre, EntidadAsociada = entidad, Orden = orden++ });
            if (Iniciales.TryGetValue(nombre, out var vals))
                foreach (var v in vals) db.Catalogos.Add(new ItemCatalogo { Tipo = nombre, Nombre = v });
            if (EstadosFijos.TryGetValue(nombre, out var est))
                foreach (var (n, f, t) in est)
                    db.Catalogos.Add(new ItemCatalogo { Tipo = nombre, Nombre = n, Estilo = "personalizado", ColorFondo = f, ColorTexto = t, Forma = "pildora", Negrita = true });
            creados = true;
        }
        if (creados) { db.SaveChanges(); db.ChangeTracker.Clear(); }
    }

    // Cargos de un texto de responsables: "Gerente / Director, Ing. Residente\nGestión Social"
    // → uno por cargo. Se corta solo en salto de línea, "/", "," y ";": un
    // "y" puede ser parte del nombre ("Gerencia del proyecto y su Equipo").
    public static List<string> Cargos(string? texto)
    {
        var res = new List<string>();
        foreach (var p in System.Text.RegularExpressions.Regex.Split(texto ?? "", @"[\n/,;]+"))
        {
            var c = System.Text.RegularExpressions.Regex.Replace(p, @"\s{2,}", " ").Trim().TrimEnd('.').Trim();
            if (c.Length == 0) continue;
            c = char.ToUpper(c[0]) + c[1..];
            if (!res.Contains(c, StringComparer.OrdinalIgnoreCase)) res.Add(c);
        }
        return res;
    }

    // Paso de arranque: los valores que ya usan los riesgos quedan en sus
    // catálogos (las listas desplegables dependen de ellos), y el texto de
    // responsables se guarda como un cargo por línea. Seguro de repetir.
    public static void SincronizarConRiesgos(AppDbContext db)
    {
        SembrarDefiniciones(db);
        var riesgos = db.Riesgos.IgnoreQueryFilters().ToList();
        var cambio = false;
        foreach (var r in riesgos)
        {
            var normal = string.Join("\n", Cargos(r.ResponsablePlan));
            if (normal != (r.ResponsablePlan ?? "")) { r.ResponsablePlan = normal; cambio = true; }
        }
        if (cambio) db.SaveChanges();
        Agregar(db, EstadoActual, riesgos.Select(r => r.EstadoActual));
        Agregar(db, Categoria, riesgos.Select(r => r.Categoria));
        Agregar(db, Area, riesgos.Select(r => r.Area));
        Agregar(db, Estrategia, riesgos.Select(r => r.Estrategia));
        Agregar(db, Cargo, riesgos.SelectMany(r => Cargos(r.ResponsablePlan)));
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    private static void Agregar(AppDbContext db, string tipo, IEnumerable<string?> valores)
    {
        var ya = db.Catalogos.IgnoreQueryFilters().AsNoTracking().Where(c => c.Tipo == tipo).Select(c => c.Nombre).ToList()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var v in valores.Select(v => (v ?? "").Trim()).Where(v => v.Length > 0))
            if (ya.Add(v)) db.Catalogos.Add(new ItemCatalogo { Tipo = tipo, Nombre = v });
    }

    // Vuelca los valores leídos del libro. Devuelve cuántos ítems nuevos quedaron.
    public static async Task<int> VolcarAsync(AppDbContext db, CatalogosLibro cat, IEnumerable<string> areas)
    {
        SembrarDefiniciones(db);
        var nuevos = 0;
        nuevos += await AgregarAsync(db, EstadoActual, cat.EstadoActual);
        nuevos += await AgregarAsync(db, Categoria, cat.Categoria);
        nuevos += await AgregarAsync(db, Estrategia, cat.Estrategia);
        nuevos += await AgregarAsync(db, EstadoPlan, cat.EstadoPlan);
        // "Área" no tiene lista de validación en el Excel: se arma con los
        // valores que realmente aparecen en las matrices importadas.
        nuevos += await AgregarAsync(db, Area, areas);
        return nuevos;
    }

    private static async Task<int> AgregarAsync(AppDbContext db, string tipo, IEnumerable<string> valores)
    {
        var limpios = valores.Select(v => (v ?? "").Trim()).Where(v => v.Length > 0)
                             .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (limpios.Count == 0) return 0;
        var ya = await db.Catalogos.AsNoTracking().Where(c => c.Tipo == tipo).Select(c => c.Nombre).ToListAsync();
        var set = ya.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = 0;
        foreach (var v in limpios)
        {
            if (set.Contains(v)) continue;
            db.Catalogos.Add(new ItemCatalogo { Tipo = tipo, Nombre = v });
            set.Add(v);
            n++;
        }
        if (n > 0) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
        return n;
    }

    // Valores activos de uno de estos catálogos, para los desplegables.
    public static List<string> Valores(AppDbContext db, string tipo) =>
        db.Catalogos.AsNoTracking().Where(c => c.Tipo == tipo && c.Activo)
          .OrderBy(c => c.Nombre).Select(c => c.Nombre).ToList();
}
