using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Planos;

// Arranque del Listado maestro de planos (seguro de repetir): las importaciones y archivos
// anteriores a la gestión por cargas quedan agrupados en cargas.
public static class MigracionPlanos
{
    public static void Ejecutar(AppDbContext db)
    {
        AdoptarCargasAnteriores(db);
        AdoptarArchivosAnteriores(db);
    }

    // Filas importadas antes: una carga por cada texto de Origen ("Listado maestro de planos — fecha…").
    private static void AdoptarCargasAnteriores(AppDbContext db)
    {
        var sueltos = db.PlanosMaestro.Where(p => p.CargaPlanosId == null && p.Origen != "").ToList();
        if (sueltos.Count == 0) return;
        foreach (var g in sueltos.GroupBy(p => p.Origen))
        {
            var carga = new CargaPlanos { NombreArchivo = g.Key, Fecha = DateTime.Now, FilasCreadas = g.Count() };
            var m = System.Text.RegularExpressions.Regex.Match(g.Key, @"(\d{2}/\d{2}/\d{4} \d{2}:\d{2})");
            if (m.Success && DateTime.TryParseExact(m.Value, "dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var f)) carga.Fecha = f;
            db.CargasPlanos.Add(carga);
            db.SaveChanges();
            foreach (var p in g) p.CargaPlanosId = carga.Id;
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Archivos cargados antes: se agrupan por minuto de carga.
    private static void AdoptarArchivosAnteriores(AppDbContext db)
    {
        var sueltos = db.ArchivosPlanoMaestro.Where(a => a.CargaArchivosPlanosId == null).ToList();
        if (sueltos.Count == 0) return;
        foreach (var g in sueltos.GroupBy(a => new DateTime(a.FechaCarga.Year, a.FechaCarga.Month, a.FechaCarga.Day, a.FechaCarga.Hour, a.FechaCarga.Minute, 0)))
        {
            var carga = new CargaArchivosPlanos
            {
                Fecha = g.Min(a => a.FechaCarga), Origen = g.Count() > 1 ? "Carga masiva" : "Archivo agregado",
                Descripcion = "Anterior a la gestión de cargas", Archivos = g.Count()
            };
            db.CargasArchivosPlanos.Add(carga);
            db.SaveChanges();
            foreach (var a in g) a.CargaArchivosPlanosId = carga.Id;
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}

// Qué archivos de un plano son los actuales: los vigentes de la versión más reciente (pueden ser
// varias hojas). Todo lo demás (vigentes de versiones anteriores y obsoletos) son versiones anteriores.
public static class ArchivosPlanos
{
    // archivos: los no eliminados de UN plano.
    public static List<ArchivoPlanoMaestro> Actuales(IEnumerable<ArchivoPlanoMaestro> archivos)
    {
        var vig = archivos.Where(a => a.Clase != ClaseArchivoDocumento.Obsoleta).ToList();
        var ultimo = vig.OrderByDescending(a => a.FechaCarga).ThenByDescending(a => a.Id).FirstOrDefault();
        if (ultimo is null) return new();
        var version = (ultimo.Version ?? "").Trim();
        return vig.Where(a => string.Equals((a.Version ?? "").Trim(), version, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.NombreOriginal).ToList();
    }

    public static List<ArchivoPlanoMaestro> Anteriores(IEnumerable<ArchivoPlanoMaestro> archivos)
    {
        var l = archivos.ToList();
        var act = Actuales(l).Select(a => a.Id).ToHashSet();
        return l.Where(a => !act.Contains(a.Id)).OrderByDescending(a => a.FechaCarga).ThenBy(a => a.NombreOriginal).ToList();
    }
}
