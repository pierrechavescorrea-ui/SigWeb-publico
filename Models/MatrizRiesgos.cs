using System.ComponentModel.DataAnnotations.Schema;

namespace SigWeb.Models;

// Una revisión de la matriz de riesgos. La matriz se revisa normalmente una
// vez al año: cada revisión es una versión nueva que nace como copia de la
// vigente, con su fecha, su responsable y su control de cambios. Las
// versiones anteriores NO se borran ni se tocan: los seguimientos y
// hallazgos registrados contra ellas siguen consultables tal como quedaron.
public class VersionMatrizRiesgos
{
    public int Id { get; set; }

    // Identificador de la revisión tal como lo use la empresa ("Rev. 01", "V2026").
    public string Codigo { get; set; } = string.Empty;

    // Fecha de la revisión (la que firma el documento, no la de captura).
    public DateTime Fecha { get; set; } = DateTime.Today;

    // Firmas del control de cambios, como vienen de la PORTADA del libro.
    public string ElaboradoPor { get; set; } = string.Empty;
    public string RevisadoPor { get; set; } = string.Empty;
    public string AprobadoPor { get; set; } = string.Empty;

    // Columna "Estado" de la portada (APCC, DPC, APC, EPA…). Se guarda tal
    // cual: es la codificación documental del cliente, no se interpreta aquí.
    public string EstadoDocumento { get; set; } = string.Empty;

    // Etapa del proyecto a la que corresponde la revisión, del nombre de la
    // hoja ("PRECONSTRUCCIÓN", "CONSTRUCCIÓN").
    public string Etapa { get; set; } = string.Empty;

    // Control de cambios: qué cambió respecto de la versión anterior.
    public string DescripcionCambios { get; set; } = string.Empty;

    // Hoja del libro Excel de la que se importó (trazabilidad del origen).
    public string HojaOrigen { get; set; } = string.Empty;

    public EstadoVersionMatriz Estado { get; set; } = EstadoVersionMatriz.Borrador;

    // Versión de la que se copió (null en la primera).
    public int? VersionAnteriorId { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Una fila de la matriz, dentro de una versión. El mismo riesgo "real"
// aparece una vez por versión: lo que lo identifica entre versiones es su
// Codigo (R-01, R-02...), y RiesgoAnteriorId encadena cada fila con la
// misma fila de la revisión anterior.
public class Riesgo
{
    public int Id { get; set; }
    public int VersionMatrizRiesgosId { get; set; }

    // Código estable entre versiones (el "item" del Excel formateado: R-01).
    public string Codigo { get; set; } = string.Empty;
    public int Orden { get; set; } = 0;

    public TipoRiesgo Tipo { get; set; } = TipoRiesgo.Riesgo;

    // Columnas de catálogo del Excel (listas de validación). Se guardan como
    // texto para no perder lo que ya está escrito si el catálogo cambia.
    public string EstadoActual { get; set; } = string.Empty;   // Activo / Cerrado
    public string Categoria { get; set; } = string.Empty;      // Externo...
    public string Area { get; set; } = string.Empty;           // Social, Ambiental, Técnica...

    // Análisis
    public string Causa { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;    // "Riesgo / Oportunidad"
    public string Consecuencia { get; set; } = string.Empty;
    public ProbabilidadRiesgo Probabilidad { get; set; } = ProbabilidadRiesgo.Moderada;
    public ImpactoRiesgo Impacto { get; set; } = ImpactoRiesgo.Importante;

    // Acciones
    public string Disparadores { get; set; } = string.Empty;
    public string Estrategia { get; set; } = string.Empty;         // Mitigar, Evitar, Mejorar...
    public string PlanRespuesta { get; set; } = string.Empty;
    public string ResponsablePlan { get; set; } = string.Empty;
    public string EstadoPlan { get; set; } = string.Empty;         // No iniciado, En ejecución...

    // Columna "Seguimiento" del Excel: cómo se hace el seguimiento a este
    // riesgo. Es la definición, no el registro de cada seguimiento (eso es
    // SeguimientoRiesgo).
    public string ComoSeHaceSeguimiento { get; set; } = string.Empty;

    // El mismo riesgo en la versión anterior de la matriz.
    public int? RiesgoAnteriorId { get; set; }

    // Calculado, no se guarda: sale siempre del cruce probabilidad × impacto.
    [NotMapped]
    public NivelRiesgo Nivel => EscalaRiesgo.Nivel(Probabilidad, Impacto);

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// A qué elemento del proyecto aplica un riesgo. Un riesgo sin alcances
// aplica a todo el proyecto. Se copia al crear una versión nueva.
public class AlcanceRiesgo
{
    public int Id { get; set; }
    public int RiesgoId { get; set; }

    // Solo uno de estos va con valor.
    public int? ElementoTrazadoId { get; set; }   // torre
    public int? ComponenteId { get; set; }        // pata u otro elemento de la torre
    public int? ItemCatalogoId { get; set; }      // ítem del catálogo "Localización"
    public int? TiroId { get; set; }              // tiro de tendido

    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}

// Un seguimiento periódico (normalmente mensual) a un riesgo: la fecha la
// elige el usuario. Deja constancia aunque el riesgo no se haya
// materializado — esa es la trazabilidad que pide el SGI.
public class SeguimientoRiesgo
{
    public int Id { get; set; }
    public int RiesgoId { get; set; }

    // Fecha del seguimiento (la elige el usuario; no es la de captura).
    public DateTime Fecha { get; set; } = DateTime.Today;

    public string Responsable { get; set; } = string.Empty;
    public ResultadoSeguimiento Resultado { get; set; } = ResultadoSeguimiento.NoMaterializado;
    public string Observacion { get; set; } = string.Empty;

    // Si en este seguimiento se cambió la valoración del riesgo, queda aquí
    // la que tenía al momento del seguimiento (para poder ver la evolución
    // sin depender de la fila del riesgo, que puede cambiar de versión).
    public ProbabilidadRiesgo? Probabilidad { get; set; }
    public ImpactoRiesgo? Impacto { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// La materialización concreta de un riesgo, registrada dentro de un
// seguimiento. Si apunta a una torre (directamente, por una de sus patas o
// por una localización ligada a ella), aparece en la pestaña "Hallazgos"
// de la hoja de vida de esa torre.
public class HallazgoRiesgo
{
    public int Id { get; set; }
    public int SeguimientoRiesgoId { get; set; }

    public int? ElementoTrazadoId { get; set; }   // torre
    public int? ComponenteId { get; set; }        // pata u otro elemento
    public int? ItemCatalogoId { get; set; }      // localización
    public int? TiroId { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public EstadoHallazgo Estado { get; set; } = EstadoHallazgo.Abierto;
    public DateTime? FechaCierre { get; set; }
    public string CerradoPor { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Soporte (acta, informe, fotografía) de un seguimiento o de un hallazgo.
// Exactamente uno de los dos Id va con valor.
public class ArchivoRiesgo
{
    public int Id { get; set; }
    public int? SeguimientoRiesgoId { get; set; }
    public int? HallazgoRiesgoId { get; set; }

    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Cada causa, consecuencia, disparador, acción del plan de respuesta o forma
// de seguimiento de un riesgo, como un elemento propio (en el Excel venían
// en una sola celda, numeradas "1. … 2. …"). El orden es el de la lista;
// no se guarda número. Los campos de texto de Riesgo (Causa, Consecuencia…)
// se mantienen como copia de estos elementos unidos por salto de línea,
// para la lista, la búsqueda y la exportación.
public class ElementoRiesgo
{
    public int Id { get; set; }
    public int RiesgoId { get; set; }
    public TipoElementoRiesgo Tipo { get; set; }
    public int Orden { get; set; }
    public string Texto { get; set; } = string.Empty;
}

// Dónde se materializó un hallazgo: una o varias torres, patas o elementos,
// localizaciones o tiros. Reemplaza los campos únicos de HallazgoRiesgo (que
// se conservan; al arrancar, lo que tengan pasa aquí).
public class LugarHallazgo
{
    public int Id { get; set; }
    public int HallazgoRiesgoId { get; set; }
    public int? ElementoTrazadoId { get; set; }
    public int? ComponenteId { get; set; }
    public int? ItemCatalogoId { get; set; }
    public int? TiroId { get; set; }
}
