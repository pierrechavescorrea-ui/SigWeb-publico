namespace SigWeb.Models;

public enum TipoAccionPlan
{
    Correccion = 0,
    AccionCorrectiva = 1,
    AccionPreventiva = 2
}

public enum EstadoAccionPlan
{
    Abierta = 0,
    EnEjecucion = 1,
    Cerrada = 2
}

// Plan de acción: se abre por un hallazgo. Puede nacer de un seguimiento a
// un riesgo (RiesgoId / SeguimientoRiesgoId) o registrarse suelto desde el
// módulo Planes de acción. Si el riesgo o el seguimiento se borran
// definitivamente, el plan queda (SetNull): es evidencia de gestión.
public class PlanAccion
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;          // PA-001
    public string DescripcionHallazgo { get; set; } = string.Empty;
    // Valores de Catálogos: "Plan de acción — origen del hallazgo" y
    // "Plan de acción — tipo de hallazgo".
    public string OrigenHallazgo { get; set; } = string.Empty;
    public string TipoHallazgo { get; set; } = string.Empty;
    // Uno o varios procesos del catálogo "Plan de acción — proceso", uno por línea.
    public string Proceso { get; set; } = string.Empty;
    // Ya no se usa: los lugares van en LugarPlan (varios por plan). Se
    // conserva la columna; al arrancar, lo que tenga pasa a LugarPlan.
    public int? ItemCatalogoId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string AnalisisCausa { get; set; } = string.Empty;

    // Quién reportó el hallazgo.
    public string ReportoNombre { get; set; } = string.Empty;
    public string ReportoCargo { get; set; } = string.Empty;
    public string ReportoEmpresa { get; set; } = string.Empty;

    public int? RiesgoId { get; set; }
    public int? SeguimientoRiesgoId { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // Carga (Excel) que lo creó. Eliminar la carga manda sus planes a la Papelera. Null = creado a mano.
    public int? CargaPlanesAccionId { get; set; }

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Una importación de Excel del listado de planes de acción.
public class CargaPlanesAccion
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string? Hoja { get; set; }
    public int? FilaEncabezados { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public int PlanesCreados { get; set; }
    public int AccionesCreadas { get; set; }
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Acción propuesta dentro de un plan.
public class AccionPlan
{
    public int Id { get; set; }
    public int PlanAccionId { get; set; }
    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Producto { get; set; } = string.Empty;
    public TipoAccionPlan Tipo { get; set; } = TipoAccionPlan.AccionCorrectiva;
    // Uno o varios cargos del catálogo "Riesgo — cargo responsable", uno por línea.
    public string Responsable { get; set; } = string.Empty;
    public EstadoAccionPlan Estado { get; set; } = EstadoAccionPlan.Abierta;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Dónde aplica el plan: una torre, una pata o elemento de torre, un ítem del
// catálogo "Localización" o un tiro (igual que el Alcance de un riesgo).
// Un plan puede tener varios. Se van con el plan y con el elemento.
public class LugarPlan
{
    public int Id { get; set; }
    public int PlanAccionId { get; set; }
    public int? ElementoTrazadoId { get; set; }   // torre
    public int? ComponenteId { get; set; }        // pata u otro elemento de la torre
    public int? ItemCatalogoId { get; set; }      // localización
    public int? TiroId { get; set; }
}

// Producto de una acción (entregable que la evidencia). Una acción puede tener varios, cada uno
// con sus anexos. AccionPlan.Producto guarda la lista de productos en texto (una línea cada uno).
public class ProductoAccion
{
    public int Id { get; set; }
    public int AccionPlanId { get; set; }
    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

// Seguimiento de una acción: qué se revisó, cuándo, quién, en qué estado quedó y sus soportes
// (ArchivoAccionPlan.SeguimientoAccionId). El último deja la acción en su EstadoNuevo.
public class SeguimientoAccion
{
    public int Id { get; set; }
    public int AccionPlanId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Observacion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public EstadoAccionPlan EstadoAnterior { get; set; }
    public EstadoAccionPlan EstadoNuevo { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Anexo del producto de una acción (acta, informe, foto…).
public class ArchivoAccionPlan
{
    public int Id { get; set; }
    public int AccionPlanId { get; set; }
    // Producto al que pertenece; null = anexos anteriores a los productos (se asignan al primero al editar).
    public int? ProductoAccionId { get; set; }
    // Soporte de un seguimiento (no es anexo de producto).
    public int? SeguimientoAccionId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

public static class EscalaAccion
{
    public static string Nombre(TipoAccionPlan t) => t switch
    {
        TipoAccionPlan.Correccion => "Corrección",
        TipoAccionPlan.AccionCorrectiva => "Acción correctiva",
        _ => "Acción preventiva"
    };

    public static string Nombre(EstadoAccionPlan e) => e switch
    {
        EstadoAccionPlan.Abierta => "Abierta",
        EstadoAccionPlan.EnEjecucion => "En ejecución",
        _ => "Cerrada"
    };

    // Mismos tonos de la matriz de riesgos: rojo / naranja / verde.
    public static string Color(EstadoAccionPlan e) => e switch
    {
        EstadoAccionPlan.Abierta => "#FF0000",
        EstadoAccionPlan.EnEjecucion => "#FFC000",
        _ => "#00B050"
    };

    public static string ColorTexto(EstadoAccionPlan e) => e == EstadoAccionPlan.Abierta ? "#ffffff" : "#1f2a3a";

    // Estado del plan, a partir del de sus acciones: sin acciones o todas
    // abiertas = Abierta; todas cerradas = Cerrada; cualquier otra mezcla =
    // En ejecución.
    public static EstadoAccionPlan EstadoPlan(IEnumerable<AccionPlan> acciones)
    {
        var l = acciones.ToList();
        if (l.Count == 0 || l.All(a => a.Estado == EstadoAccionPlan.Abierta)) return EstadoAccionPlan.Abierta;
        if (l.All(a => a.Estado == EstadoAccionPlan.Cerrada)) return EstadoAccionPlan.Cerrada;
        return EstadoAccionPlan.EnEjecucion;
    }
}

// Riesgos a los que se asocia un plan (uno o varios). PlanAccion.RiesgoId guarda el primero
// (lo usan el seguimiento de origen y la Papelera); esta lista manda para mostrar y filtrar.
public class RiesgoPlan
{
    public int Id { get; set; }
    public int PlanAccionId { get; set; }
    public int RiesgoId { get; set; }
    public int Orden { get; set; }
}

// Carpeta de adjuntos de un plan (columna "Adjuntos" del listado): nombre libre, varios archivos.
public class CarpetaPlan
{
    public int Id { get; set; }
    public int PlanAccionId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

public class ArchivoCarpetaPlan
{
    public int Id { get; set; }
    public int CarpetaPlanId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
