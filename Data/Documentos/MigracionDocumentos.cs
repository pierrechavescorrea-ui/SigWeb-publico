using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Documentos;

// Arranque del Listado maestro de documentos. Todo es seguro de repetir en cada inicio.
public static class MigracionDocumentos
{
    public const string Tabla = "documentos";
    // Catálogos (Utilidades › Catálogos) de donde salen las listas de Tipo documental y Estado.
    public const string CatTipo = "Tipo documental";
    public const string CatEstado = "Estado del documento";
    // Nombres con que se crearon el 15/10/2026 (se renombran al arrancar).
    private const string CatTipoAnterior = "Documentos — tipo documental";
    private const string CatEstadoAnterior = "Documentos — estado";

    // Valores dados por el usuario (15/10/2026). (Nombre, Descripción)
    private static readonly (string Nombre, string Descripcion)[] TiposIniciales =
    {
        ("Formatos", ""), ("Documentos externos", ""), ("Procedimientos e instructivos", ""), ("Informes", ""),
    };
    private static readonly (string Nombre, string Descripcion)[] EstadosIniciales =
    {
        ("APC", "Aprobado para construcción"), ("En revisión interna", ""), ("En revisión de interventoría", ""), ("Obsoleto", ""),
    };

    public static void Ejecutar(AppDbContext db)
    {
        SembrarDefiniciones(db);
        AdoptarCargasAnteriores(db);
        AdoptarArchivosAnteriores(db);
        CrearRevisiones(db);
        MoverEstadoDelExcel(db);
        AjustarACatalogo(db);
    }

    // Crea (o renombra desde el nombre anterior) los dos catálogos y les carga los valores
    // iniciales una sola vez: si luego se borran en Catálogos, no vuelven.
    private static void SembrarDefiniciones(AppDbContext db)
    {
        Sembrar(db, CatTipo, CatTipoAnterior, "Listado maestro de documentos — tipo documental", 40, TiposIniciales);
        Sembrar(db, CatEstado, CatEstadoAnterior, "Listado maestro de documentos — estado del documento", 41, EstadosIniciales);
    }

    private static void Sembrar(AppDbContext db, string nombre, string anterior, string entidad, int orden, (string Nombre, string Descripcion)[] iniciales)
    {
        var defs = db.DefinicionesCatalogo.IgnoreQueryFilters().ToList();
        if (defs.Any(d => string.Equals(d.Nombre, nombre, StringComparison.OrdinalIgnoreCase))) return;

        var vieja = defs.FirstOrDefault(d => string.Equals(d.Nombre, anterior, StringComparison.OrdinalIgnoreCase));
        if (vieja is not null)
        {
            vieja.Nombre = nombre;
            vieja.EntidadAsociada = entidad;
            foreach (var i in db.Catalogos.Where(i => i.Tipo == anterior)) i.Tipo = nombre;
            foreach (var c in db.CamposTabla.Where(c => c.CatalogoTipo == anterior)) c.CatalogoTipo = nombre;
        }
        else
        {
            db.DefinicionesCatalogo.Add(new DefinicionCatalogo { Nombre = nombre, EntidadAsociada = entidad, Orden = orden });
        }
        db.SaveChanges();

        var ya = db.Catalogos.Where(i => i.Tipo == nombre).ToList();
        foreach (var (n, desc) in iniciales)
        {
            var item = ya.FirstOrDefault(i => string.Equals(i.Nombre.Trim(), n, StringComparison.OrdinalIgnoreCase));
            if (item is null) db.Catalogos.Add(new ItemCatalogo { Tipo = nombre, Nombre = n, Descripcion = desc });
            else if (string.IsNullOrWhiteSpace(item.Descripcion) && desc != "") item.Descripcion = desc;
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Filas importadas antes de que existieran las cargas: se agrupan por libro/hoja en una
    // carga cada una. Son las primeras que cargó el usuario y, por indicación suya, quedan
    // con Tipo documental "Formatos".
    private static void AdoptarCargasAnteriores(AppDbContext db)
    {
        var sueltos = db.DocumentosMaestro.IgnoreQueryFilters()
            .Where(d => d.CargaDocumentosId == null && d.OrigenImportacion != null).ToList();
        if (sueltos.Count == 0) return;
        foreach (var g in sueltos.GroupBy(d => d.OrigenImportacion!))
        {
            var partes = g.Key.Split(" › ", 2);
            var carga = new CargaDocumentos
            {
                NombreArchivo = partes[0],
                Hoja = partes.Length > 1 ? partes[1] : null,
                Fecha = g.Min(d => d.FechaCreacion),
                FilasCreadas = g.Count()
            };
            db.CargasDocumentos.Add(carga);
            db.SaveChanges();
            foreach (var d in g)
            {
                d.CargaDocumentosId = carga.Id;
                if (string.IsNullOrWhiteSpace(d.TipoDocumental)) d.TipoDocumental = "Formatos";
            }
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Vigentes y editables sin fila de control de cambios: se agrupan por documento + versión en una
    // RevisionDocumento (fecha = la del archivo; cambio y responsable = los de su control de cambios
    // anterior). Idempotente: si la revisión de esa versión ya existe, el archivo se le asigna.
    public static void CrearRevisiones(AppDbContext db)
    {
        var sueltos = db.VersionesDocumento
            .Where(v => v.RevisionDocumentoId == null && v.Clase != ClaseArchivoDocumento.Obsoleta).ToList();
        if (sueltos.Count == 0) return;
        var idsDocs = sueltos.Select(v => v.DocumentoMaestroId).Distinct().ToList();
        var existentes = db.RevisionesDocumento.Where(r => idsDocs.Contains(r.DocumentoMaestroId)).ToList();
        var idsVer = sueltos.Select(v => v.Id).ToList();
        var cambios = db.ControlesCambioVersion.AsNoTracking().Where(c => idsVer.Contains(c.VersionDocumentoId)).ToList()
            .GroupBy(c => c.VersionDocumentoId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList());
        var nuevas = new List<(RevisionDocumento Rev, List<VersionDocumento> Archivos)>();
        foreach (var g in sueltos.GroupBy(v => (v.DocumentoMaestroId, Ver: (v.Version ?? "").Trim().ToUpperInvariant())))
        {
            var archivos = g.OrderBy(v => v.FechaCarga).ToList();
            var rev = existentes.FirstOrDefault(r => r.DocumentoMaestroId == g.Key.DocumentoMaestroId
                && (r.Version ?? "").Trim().ToUpperInvariant() == g.Key.Ver);
            if (rev is null)
            {
                var cc = archivos.SelectMany(a => cambios.TryGetValue(a.Id, out var l) ? l : new()).ToList();
                var obs = archivos.Select(a => a.Observaciones).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().ToList();
                rev = new RevisionDocumento
                {
                    DocumentoMaestroId = g.Key.DocumentoMaestroId,
                    Version = archivos.Select(a => a.Version?.Trim()).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                    Fecha = archivos.Max(a => a.Fecha) ?? cc.Max(c => c.Fecha),
                    Cambio = cc.Count > 0 ? string.Join("\n", cc.Select(c => c.Cambio)) : obs.Count > 0 ? string.Join("\n", obs) : null,
                    Responsable = cc.Select(c => c.Responsable).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)),
                    Creada = archivos.Max(a => a.FechaCarga)
                };
                db.RevisionesDocumento.Add(rev);
                existentes.Add(rev);
            }
            nuevas.Add((rev, archivos));
        }
        db.SaveChanges();
        foreach (var (rev, archivos) in nuevas)
            foreach (var a in archivos) a.RevisionDocumentoId = rev.Id;
        db.SaveChanges();
    }

    // Archivos cargados antes de que existieran las cargas de archivos: se agrupan por minuto de
    // carga (lo que se subió junto queda en la misma carga).
    private static void AdoptarArchivosAnteriores(AppDbContext db)
    {
        var sueltos = db.VersionesDocumento.IgnoreQueryFilters().Where(v => v.CargaArchivosDocumentosId == null).ToList();
        if (sueltos.Count == 0) return;
        foreach (var g in sueltos.GroupBy(v => new DateTime(v.FechaCarga.Year, v.FechaCarga.Month, v.FechaCarga.Day, v.FechaCarga.Hour, v.FechaCarga.Minute, 0)))
        {
            var carga = new CargaArchivosDocumentos
            {
                Fecha = g.Min(v => v.FechaCarga), Origen = g.Count() > 1 ? "Carga masiva" : "Nueva versión",
                Descripcion = "Anterior a la gestión de cargas", Archivos = g.Count()
            };
            db.CargasArchivosDocumentos.Add(carga);
            db.SaveChanges();
            foreach (var v in g) v.CargaArchivosDocumentosId = carga.Id;
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Si un Excel trajo una columna "Estado", su valor pasa a la columna propia Estado
    // (la que filtra) y esa columna del Excel se oculta (no se borra).
    private static void MoverEstadoDelExcel(AppDbContext db)
    {
        var campos = db.CamposTabla.Where(c => c.Tabla == Tabla && !c.Eliminado && c.Origen != OrigenCampoTabla.Nativo).ToList();
        foreach (var (campo, nativo) in campos.Select(c => (c, Nativo(c.Nombre))).Where(x => x.Item2 is not null).ToList())
        {
            var valores = db.ValoresCampoTabla.Where(v => v.CampoTablaId == campo.Id && v.Valor != null).ToList();
            if (valores.Count > 0)
            {
                var ids = valores.Select(v => v.FilaId).ToList();
                var docs = db.DocumentosMaestro.IgnoreQueryFilters().Where(d => ids.Contains(d.Id)).ToDictionary(d => d.Id);
                foreach (var v in valores)
                {
                    if (!docs.TryGetValue(v.FilaId, out var d)) continue;
                    if (nativo == "Estado" && string.IsNullOrWhiteSpace(d.Estado)) d.Estado = v.Valor!.Trim();
                    if (nativo == "TipoDocumental" && string.IsNullOrWhiteSpace(d.TipoDocumental)) d.TipoDocumental = v.Valor!.Trim();
                }
            }
            campo.Visible = false;
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Columna del Excel que corresponde a una columna propia: "Estado" → Estado, "Tipo documental" → TipoDocumental.
    public static string? Nativo(string encabezado)
    {
        var n = Normalizar(encabezado);
        return n switch
        {
            "estado" or "estado del documento" => "Estado",
            "tipo documental" or "tipo de documento" => "TipoDocumental",
            _ => null
        };
    }

    // Tipo documental y Estado salen del catálogo: cada valor guardado se lleva al nombre exacto
    // del ítem que le corresponde (sin importar mayúsculas/tildes; también por la descripción,
    // ej. "Aprobado para construcción" → "APC"). Lo que no corresponde a ningún ítem se deja
    // como está (se ve en la tabla y en el filtro) para que el usuario lo corrija.
    public static void AjustarACatalogo(AppDbContext db)
    {
        var tipos = Diccionario(db, CatTipo);
        var estados = Diccionario(db, CatEstado);
        var cambios = false;
        foreach (var d in db.DocumentosMaestro.IgnoreQueryFilters().Where(d => d.TipoDocumental != null || d.Estado != null))
        {
            var t = Canonico(d.TipoDocumental, tipos);
            var e = Canonico(d.Estado, estados);
            if (t != d.TipoDocumental) { d.TipoDocumental = t; cambios = true; }
            if (e != d.Estado) { d.Estado = e; cambios = true; }
        }
        if (cambios) db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Clave normalizada (nombre o descripción) → nombre del ítem.
    public static Dictionary<string, string> Diccionario(AppDbContext db, string catalogo)
    {
        var r = new Dictionary<string, string>();
        foreach (var i in db.Catalogos.AsNoTracking().Where(i => i.Tipo == catalogo && i.Activo).ToList())
        {
            r.TryAdd(Normalizar(i.Nombre), i.Nombre);
            if (!string.IsNullOrWhiteSpace(i.Descripcion)) r.TryAdd(Normalizar(i.Descripcion), i.Nombre);
        }
        return r;
    }

    public static string? Canonico(string? valor, Dictionary<string, string> dic) =>
        string.IsNullOrWhiteSpace(valor) ? null : dic.TryGetValue(Normalizar(valor), out var n) ? n : valor.Trim();

    public static string Normalizar(string s) =>
        new string(System.Text.RegularExpressions.Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ")
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());

    // Siguiente versión: incrementa el último número ("2" → "3", "Rev. 04" → "Rev. 05", "1.2" → "1.3"); sin número → "1".
    public static string SiguienteVersion(string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual)) return "1";
        var m = System.Text.RegularExpressions.Regex.Match(actual, @"(\d+)(?!.*\d)");
        if (!m.Success) return actual.Trim() + " 1";
        var n = (long.Parse(m.Value) + 1).ToString().PadLeft(m.Value.Length, '0');
        return actual[..m.Index] + n + actual[(m.Index + m.Length)..];
    }
}
