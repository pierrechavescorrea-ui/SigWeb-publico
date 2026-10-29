namespace SigWeb.Models;

// Fila del Listado maestro de documentos. Sus columnas son "Campos" de la tabla
// "documentos" (CampoTabla/ValorCampoTabla): las trae el Excel que se importa
// (origen Excel) o las crea el usuario (Manual). Columnas propias: Código (si al
// importar se elige la columna del código, las filas con el mismo código se
// actualizan en vez de duplicarse), Tipo documental y Estado (filtros, listas de
// Catálogos) y el adjunto vigente (VersionDocumento más reciente).
public class DocumentoMaestro
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public string? TipoDocumental { get; set; }
    public string? Estado { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    // Libro/hoja de la importación que creó esta fila (texto; la carga en sí es CargaDocumentosId).
    public string? OrigenImportacion { get; set; }
    // Carga (Excel) que creó la fila. Eliminar la carga envía sus filas a la Papelera. Null = creada a mano.
    public int? CargaDocumentosId { get; set; }

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Una importación de Excel que alimenta el listado maestro de documentos.
public class CargaDocumentos
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string? Hoja { get; set; }
    public int? FilaEncabezados { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public int FilasCreadas { get; set; }
    public int FilasActualizadas { get; set; }

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Qué es cada archivo de un documento: la versión vigente (la que se consulta), su versión
// editable (Word/Excel fuente) o una versión obsoleta cargada como historial.
public enum ClaseArchivoDocumento { Vigente = 0, Editable = 1, Obsoleta = 2 }

// Un archivo de un documento del listado maestro. El actual es el Vigente más reciente
// (mayor FechaCarga) y el editable, el Editable más reciente; todos los demás (vigentes
// reemplazados, editables anteriores y obsoletos) son versiones anteriores.
public class VersionDocumento
{
    public int Id { get; set; }
    public int DocumentoMaestroId { get; set; }
    public ClaseArchivoDocumento Clase { get; set; } = ClaseArchivoDocumento.Vigente;
    // Carga de archivos en la que entró (carga masiva o "Nueva versión"). Eliminar la carga manda sus archivos a la Papelera.
    public int? CargaArchivosDocumentosId { get; set; }
    // Fila del control de cambios (revisión) a la que pertenece. Null = obsoleta u otra sin revisión.
    public int? RevisionDocumentoId { get; set; }
    public string? Version { get; set; }
    public DateTime? Fecha { get; set; }
    // Ya no se usa en la interfaz (lo reemplaza ControlCambioVersion); se conserva por los datos ya guardados.
    public string? Observaciones { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Fila del control de cambios de un documento: una revisión (versión, fecha, cambio, responsable)
// con sus archivos vigente(s) y editable(s) (VersionDocumento.RevisionDocumentoId). El listado
// muestra los de la revisión más reciente (mayor fecha; a igual fecha, la última creada).
public class RevisionDocumento
{
    public int Id { get; set; }
    public int DocumentoMaestroId { get; set; }
    public string? Version { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Cambio { get; set; }
    public string? Responsable { get; set; }
    public DateTime Creada { get; set; } = DateTime.Now;
}

// Ya no se usa en la interfaz (lo reemplaza RevisionDocumento); se conserva por los datos guardados
// y MigracionDocumentos los pasa a la revisión correspondiente.
// Fila del control de cambios de una versión de documento (fecha, cambio, responsable).
public class ControlCambioVersion
{
    public int Id { get; set; }
    public int VersionDocumentoId { get; set; }
    public DateTime? Fecha { get; set; }
    public string Cambio { get; set; } = string.Empty;
    public string? Responsable { get; set; }
    public int Orden { get; set; }
}

// Una carga de archivos del listado maestro de documentos: una carga masiva o una "Nueva versión".
// Agrupa los archivos (VersionDocumento) que entraron juntos para poder revisarlos o eliminarlos juntos.
public class CargaArchivosDocumentos
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    // "Carga masiva" o "Nueva versión"; Descripcion: código del documento o resumen.
    public string Origen { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Archivos { get; set; }

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
