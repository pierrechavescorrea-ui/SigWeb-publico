using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.ListadoMateriales;

// Arranque de Materiales (16/10/2026): el listado pasa a ser solo Campos + "Adjuntos".
// Seguro de repetir en cada inicio.
//  1) PDF de remisión / certificado y fotos → AdjuntoMaterial (Remisión, Certificado, Registro fotográfico).
//  3) Material.ComponenteId → UbicacionMaterial.
//  2) Columnas fijas con datos → Campos de la tabla "materiales" (conservan nombre, orden, visibilidad
//     y formatos: la misma fila de CampoTabla deja de ser "propia"); las vacías quedan ocultas.
public static class MigracionMateriales
{
    public const string Tabla = "materiales";

    // Clave de la columna propia → (nombre si nunca se creó, tipo, valor).
    private static readonly (string Clave, string Nombre, TipoCampoTabla Tipo, Func<Material, string?> Valor)[] Fijas =
    {
        ("Fecha", "Fecha", TipoCampoTabla.Fecha, m => m.Fecha.ToString("yyyy-MM-dd")),
        ("Localizacion", "Localización", TipoCampoTabla.Texto, m => m.Localizacion),
        ("NombreMaterial", "Material", TipoCampoTabla.Texto, m => m.NombreMaterial),
        ("NumeroRemision", "Remisión", TipoCampoTabla.Texto, m => m.NumeroRemision),
        ("NumeroCertificado", "Cert. Calidad", TipoCampoTabla.Texto, m => m.NumeroCertificado),
        ("Proveedor", "Proveedor / Fabricante", TipoCampoTabla.Texto, m => m.Proveedor),
        ("AprobadoPorInterventoria", "Aprobado", TipoCampoTabla.SiNo, m => m.AprobadoPorInterventoria ? "1" : null),
        ("Lote", "Lote", TipoCampoTabla.Texto, m => m.Lote),
        ("CriterioAceptacion", "Criterio de aceptación", TipoCampoTabla.Texto, m => m.CriterioAceptacion),
        ("DocumentoReferencia", "Documento de referencia", TipoCampoTabla.Texto, m => m.DocumentoReferencia),
        ("CodigoComunicadoEnvio", "Cód. comunicado envío", TipoCampoTabla.Texto, m => m.CodigoComunicadoEnvio),
        ("FechaAprobacionInterventoria", "Fecha aprob. interventoría", TipoCampoTabla.Fecha, m => m.FechaAprobacionInterventoria?.ToString("yyyy-MM-dd")),
        ("CodigoComunicacionAprobacion", "Cód. com. aprob.", TipoCampoTabla.Texto, m => m.CodigoComunicacionAprobacion),
        ("Observaciones", "Observaciones", TipoCampoTabla.Texto, m => m.Observaciones),
    };

    public static void Ejecutar(AppDbContext db)
    {
        try
        {
            PasarArchivos(db);
            PasarColumnas(db);
            PasarUbicaciones(db);
        }
        catch (Exception ex)
        {
            // Sin la migración de base de datos aplicada no hay AdjuntosMaterial/EnCampos: no se toca nada.
            Console.WriteLine("MigracionMateriales: " + ex.Message);
        }
        finally { db.ChangeTracker.Clear(); }
    }

    private static void PasarArchivos(AppDbContext db)
    {
        var mats = db.Materiales.IgnoreQueryFilters().Where(m => m.RutaRemisionPdf != null || m.RutaCertificadoPdf != null).ToList();
        var fotos = db.FotosMateriales.ToList();
        if (mats.Count == 0 && fotos.Count == 0) return;
        foreach (var m in mats)
        {
            if (!string.IsNullOrWhiteSpace(m.RutaRemisionPdf)) Agregar(db, m.Id, AdjuntoMaterial.Remision, m.RutaRemisionPdf!, m.Fecha);
            if (!string.IsNullOrWhiteSpace(m.RutaCertificadoPdf)) Agregar(db, m.Id, AdjuntoMaterial.Certificado, m.RutaCertificadoPdf!, m.Fecha);
            m.RutaRemisionPdf = null; m.RutaCertificadoPdf = null;
        }
        foreach (var f in fotos)
        {
            Agregar(db, f.MaterialId, AdjuntoMaterial.Fotografico, f.RutaArchivo, f.FechaCarga);
            db.FotosMateriales.Remove(f);   // el archivo en disco sigue: ahora lo referencia el adjunto
        }
        db.SaveChanges();
    }

    // Material.ComponenteId (una sola pata) → UbicacionMaterial (torre + elemento); el campo queda vacío.
    private static void PasarUbicaciones(AppDbContext db)
    {
        var mats = db.Materiales.IgnoreQueryFilters().Where(m => m.ComponenteId != null).ToList();
        if (mats.Count == 0) return;
        var ids = mats.Select(m => m.ComponenteId!.Value).Distinct().ToList();
        var torreDe = db.Componentes.IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id, c => c.ElementoTrazadoId);
        foreach (var m in mats)
        {
            var cid = m.ComponenteId!.Value;
            if (torreDe.TryGetValue(cid, out var t) && !db.UbicacionesMaterial.Any(u => u.MaterialId == m.Id && u.ComponenteId == cid))
                db.UbicacionesMaterial.Add(new UbicacionMaterial { MaterialId = m.Id, ComponenteId = cid, ElementoTrazadoId = t, Fecha = m.Fecha });
            m.ComponenteId = null;
        }
        db.SaveChanges();
    }

    private static void Agregar(AppDbContext db, int materialId, string clase, string ruta, DateTime fecha) =>
        db.AdjuntosMaterial.Add(new AdjuntoMaterial
        {
            MaterialId = materialId, Clase = clase, RutaArchivo = ruta, FechaCarga = fecha,
            NombreOriginal = Path.GetFileName(ruta.Replace('\\', '/'))
        });

    private static void PasarColumnas(AppDbContext db)
    {
        var pendientes = db.Materiales.IgnoreQueryFilters().Where(m => !m.EnCampos).ToList();
        var campos = db.CamposTabla.IgnoreQueryFilters().Where(c => c.Tabla == Tabla).ToList();
        var nativas = campos.Where(c => c.Origen == OrigenCampoTabla.Nativo && Fijas.Any(f => f.Clave == c.Clave)).ToList();
        if (pendientes.Count == 0 && nativas.Count == 0) return;

        int orden = campos.Count == 0 ? 0 : campos.Max(c => c.Orden) + 1;
        foreach (var (clave, nombre, tipo, valor) in Fijas)
        {
            var conDato = pendientes.Where(m => !string.IsNullOrWhiteSpace(valor(m))).ToList();
            var c = nativas.FirstOrDefault(x => x.Clave == clave);
            if (conDato.Count == 0) continue;   // vacía: queda como propia no declarada (oculta)
            if (c is null)
            {
                c = new CampoTabla { Tabla = Tabla, Nombre = nombre, Tipo = tipo, Orden = orden++, Visible = true };
                db.CamposTabla.Add(c);
            }
            c.Origen = OrigenCampoTabla.Manual;
            c.Clave = MotorTabla.ClaveNueva("c");
            db.SaveChanges();
            var ya = db.ValoresCampoTabla.Where(v => v.CampoTablaId == c.Id).Select(v => v.FilaId).ToHashSet();
            foreach (var m in conDato.Where(m => !ya.Contains(m.Id)))
                db.ValoresCampoTabla.Add(new ValorCampoTabla { CampoTablaId = c.Id, FilaId = m.Id, Valor = valor(m)!.Trim() });
        }
        foreach (var m in pendientes) m.EnCampos = true;
        db.SaveChanges();
    }
}
