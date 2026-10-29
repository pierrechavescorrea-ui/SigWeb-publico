namespace SigWeb.Models;

// Un PDF cargado para una fila del listado maestro de planos. Puede haber
// varios por fila (un mismo código a veces agrupa varias hojas/páginas
// del mismo diseño, ej. "DIS4025" con H25, H26, H27 como archivos
// separados), así que esto es una lista, igual que ArchivoComponente.
public class ArchivoPlanoMaestro
{
    public int Id { get; set; }
    public int PlanoMaestroId { get; set; }

    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;

    // Vigente u Obsoleta (los planos no guardan editables). Los actuales del plano son los
    // vigentes de la versión más reciente (puede haber varias hojas); el resto, anteriores.
    public ClaseArchivoDocumento Clase { get; set; } = ClaseArchivoDocumento.Vigente;
    public string? Version { get; set; }
    public DateTime? Fecha { get; set; }
    // Carga de archivos en la que entró (carga masiva o archivo agregado a un plano).
    public int? CargaArchivosPlanosId { get; set; }

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
