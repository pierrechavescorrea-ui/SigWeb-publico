namespace SigWeb.Models;

// Una fila del "listado maestro de planos": el registro oficial de cada
// plano/documento del proyecto (código, título, revisión vigente, etc.),
// tal como se importa desde el Excel de control documental. El PDF real
// se vincula a esta fila (por código, automático o manual) en vez de
// subirse suelto en cada torre/pata, para no repetir el mismo archivo
// cientos de veces cuando varios elementos comparten el mismo plano tipo
// (ej. trinchos, cunetas, gaviones).
public class PlanoMaestro
{
    public int Id { get; set; }

    // Código del listado (ej. "GEB-U113-IN000000-L000-DIS4025"). Puede
    // repetirse una sola vez por fila; se usa para emparejar con el
    // nombre de archivo del PDF cargado.
    public string Codigo { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
    public DateTime? Fecha { get; set; }
    public string RevisionVigente { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? Notas { get; set; }
    public string? RevisionAnterior { get; set; }

    // Los archivos PDF vinculados a esta fila NO van aquí: un mismo código
    // del listado puede agrupar varias hojas/páginas (ej. "DIS4025" trae
    // H25, H26, H27 como PDF separados), así que pueden ser varios. Ver
    // ArchivoPlanoMaestro, que apunta a esta fila por Id.

    // De qué carga de Excel vino este registro (ej. "Listado maestro de
    // planos — 29/09/2026"). Se sobrescribe cada vez que se reimporta.
    public string Origen { get; set; } = string.Empty;

    // Disciplina (Estructural, Civil, Eléctrico…). Null = sin clasificar (solo aparece en "Todos").
    public int? GrupoPlanoId { get; set; }

    // Carga (Excel) que creó la fila. Eliminar la carga envía sus filas a la Papelera. Null = creada a mano o anterior.
    public int? CargaPlanosId { get; set; }

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Una importación de Excel del listado maestro de planos.
public class CargaPlanos
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

// Una carga de archivos (PDF) del listado maestro de planos: carga masiva o archivos agregados a un plano.
public class CargaArchivosPlanos
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Origen { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Archivos { get; set; }
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
