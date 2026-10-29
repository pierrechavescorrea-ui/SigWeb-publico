namespace SigWeb.Models;

// Una fila de resultados de un Tipo de Ensayo (ej. la rotura de un
// cilindro de concreto). Torre y patas quedan aparte de la plantilla
// flexible (ValorCampoEnsayo) porque tienen lógica propia de cruce con
// Trazado: Trazado solo CONSULTA estos resultados por Torre + Pata, no
// hace falta cargarlos dos veces.
public class ResultadoEnsayo
{
    public int Id { get; set; }
    public int TipoEnsayoId { get; set; }

    // Torre a la que pertenece. Null cuando el valor de la columna
    // "Torre" del Excel no se pudo emparejar con ninguna torre
    // existente: el resultado igual se guarda, solo que no aparece en
    // Trazado hasta que alguien lo asocie manualmente.
    public int? ElementoTrazadoId { get; set; }

    // Texto tal cual venía en la columna "Elemento" (ej. "BC",
    // "B (PED)", "CUNETA M1"), para trazabilidad y para poder revisarlo
    // si el parseo automático de patas falló.
    public string ElementoTexto { get; set; } = string.Empty;

    public SubElementoResultado SubElemento { get; set; } = SubElementoResultado.Ninguno;

    public int? InformeId { get; set; }

    // Diseño de mezcla: interino hasta que exista el Listado Maestro de
    // documentos externos (código de referencia + PDF adjunto directo
    // aquí mismo). El día que exista ese módulo, se migra a una
    // referencia real sin perder el histórico.
    public string? DisenoMezclaCodigo { get; set; }
    public string? DisenoMezclaArchivo { get; set; }

    // Para Tipos de Ensayo que NO usan Torre (TipoEnsayo.UsaTorre = false):
    // ubicación en texto libre (progresiva, sitio, abscisa...) en vez de
    // una Torre del trazado, y fecha en que se ejecutó el ensayo en campo.
    public string? Localizacion { get; set; }
    public DateTime? FechaEjecucion { get; set; }

    // "Manual", o "Excel <nombre de archivo> - <fecha de carga>".
    public string Origen { get; set; } = "Manual";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
