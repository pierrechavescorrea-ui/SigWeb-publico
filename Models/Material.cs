namespace SigWeb.Models;

public class Material
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Localizacion { get; set; } = string.Empty;
    public string NombreMaterial { get; set; } = string.Empty;

    public string NumeroRemision { get; set; } = string.Empty;
    public string? RutaRemisionPdf { get; set; }

    public string NumeroCertificado { get; set; } = string.Empty;
    public string? RutaCertificadoPdf { get; set; }

    public string Proveedor { get; set; } = string.Empty;
    public string Lote { get; set; } = string.Empty;
    public string CriterioAceptacion { get; set; } = string.Empty;
    public string DocumentoReferencia { get; set; } = string.Empty;
    public string CodigoComunicadoEnvio { get; set; } = string.Empty;
    public bool AprobadoPorInterventoria { get; set; }
    public DateTime? FechaAprobacionInterventoria { get; set; }
    public string CodigoComunicacionAprobacion { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;

    // ---------- Papelera de reciclaje ----------
    // Al "eliminar" un registro no se borra de la base de datos: se marca
    // Eliminado = true y se guarda la fecha. Así puede restaurarse hasta 30
    // días después desde el módulo Papelera; pasado ese plazo se purga
    // automáticamente (borrado físico definitivo, incluyendo sus archivos).
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }

    // ---------- Vínculo con el trazado (KMZ) ----------
    // Opcional: a qué pata (u otro componente) del trazado pertenece este
    // material. Null si aún no se ha ubicado, o si el proyecto no usa esta función.
    public int? ComponenteId { get; set; }

    // true = sus datos ya están en Campos (tabla "materiales"); las columnas fijas de arriba quedan
    // solo como copia histórica. Las filas nuevas nacen así (MigracionMateriales pasa las anteriores).
    public bool EnCampos { get; set; }

    // Carga (Excel) que lo creó. Eliminar la carga envía sus materiales a la Papelera. Null = creado a mano.
    public int? CargaMaterialesId { get; set; }
}

// Una importación de Excel del listado de materiales.
public class CargaMateriales
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string? Hoja { get; set; }
    public int? FilaEncabezados { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public int FilasCreadas { get; set; }
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}