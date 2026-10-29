namespace SigWeb.Models;

// Un comunicado del listado de comunicados (cartas/oficios con el cliente, la interventoría…).
// Las columnas propias son las que necesita la lógica de seguimiento (estado, respuesta, plazo);
// el resto de columnas del Excel llegan como Campos (tabla "comunicados").
public class Comunicado
{
    public int Id { get; set; }
    public string? Tipo { get; set; }                 // Externa, Interna…
    public string? Remitente { get; set; }
    public string? Sentido { get; set; }              // "Entrada" | "Salida"
    public string Consecutivo { get; set; } = string.Empty;
    public DateTime? Fecha { get; set; }
    public string? Descripcion { get; set; }
    public bool? RequiereRespuesta { get; set; }      // null = sin definir
    // Consecutivo del comunicado que lo responde. Vacío + requiere respuesta = Pendiente.
    public string? ConsecutivoRespuesta { get; set; }
    // Fecha de la respuesta digitada; si está vacía se toma la del comunicado de respuesta del listado.
    public DateTime? FechaRespuesta { get; set; }
    // Áreas que deben responder (Técnica, SST, Calidad…), separadas por coma.
    public string? ResponsableRespuesta { get; set; }
    public string? Observaciones { get; set; }
    // Respuesta sugerida que el usuario descartó (no se vuelve a sugerir).
    public string? SugerenciaDescartada { get; set; }

    public int? CargaComunicadosId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Archivo o enlace a la nube (Drive, OneDrive, SharePoint…) de un comunicado.
public class AdjuntoComunicado
{
    public const string Archivo = "Archivo";
    public const string Nube = "Nube";

    public int Id { get; set; }
    public int ComunicadoId { get; set; }
    public string Clase { get; set; } = Archivo;      // Archivo | Nube
    public string Nombre { get; set; } = string.Empty; // nombre original del archivo o título del enlace
    public string Ruta { get; set; } = string.Empty;   // /uploads/... o la dirección https://
    public DateTime FechaCarga { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Una importación de Excel del listado de comunicados.
public class CargaComunicados
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string? Hoja { get; set; }
    public int? FilaEncabezados { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public int FilasCreadas { get; set; }
    public int FilasActualizadas { get; set; }
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
