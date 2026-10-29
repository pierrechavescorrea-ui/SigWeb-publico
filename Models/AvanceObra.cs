namespace SigWeb.Models;

// ---------- Avance de obra (informe diario F-054) ----------
// Avance FÍSICO de la obra por torre y por pata (m³, kg, t, %), aparte de la Matriz PIE
// (que mide documentación y aprobaciones). Lo digitado en el informe del día alimenta el
// mapa de calor, la vista torre por torre con su visor 3D y la matriz de avance acumulado.

// A qué nivel se registra una actividad de obra.
public enum AmbitoAvance
{
    Torre = 0,
    Pata = 1
}

// Qué dibuja el visor 3D con el avance de la actividad. Ninguno = solo cuenta en el %.
public enum EfectoAvance3D
{
    Ninguno = 0,
    Excavacion = 1,
    Solado = 2,
    Nivelacion = 3,   // stub o parrilla nivelados
    Acero = 4,
    Concreto = 5,
    Relleno = 6,
    Prearmado = 7,
    Montaje = 8,
    Verticalidad = 9,
    Vestida = 10
}

// Catálogo de actividades de obra (editable). Peso = fracción del avance total de la torre
// (los de fábrica salen de la fila 1 de la hoja "Avance acumulado" del F-054).
public class ActividadObra
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Grupo { get; set; } = string.Empty;        // Obra civil, SPT, Montaje...
    public AmbitoAvance Ambito { get; set; } = AmbitoAvance.Pata;
    public decimal Peso { get; set; }
    public string? Unidad { get; set; }                     // m³, kg, t, sitio...
    public string? ItemContrato { get; set; }               // ítem de la lista de cantidades (ej. 2.1.1)
    public EfectoAvance3D Efecto { get; set; } = EfectoAvance3D.Ninguno;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Un informe por día: encabezado del F-054.
public class InformeDiario
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string? ClimaManana { get; set; }
    public string? ClimaTarde { get; set; }
    public decimal? HorasLluvia { get; set; }
    public string? ElaboradoPor { get; set; }
    public string? RevisadoPor { get; set; }
    public string? AprobadoPor { get; set; }
    public string? Observaciones { get; set; }

    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// "Actividades complementarias" del F-054 (técnico, social, ambiental, topografía...).
public class NotaInformeArea
{
    public int Id { get; set; }
    public int InformeDiarioId { get; set; }
    public string Area { get; set; } = string.Empty;
    public string? Realizadas { get; set; }
    public string? Programadas { get; set; }
    public int Orden { get; set; }
}

// Lo ejecutado (o programado) en un día para una torre, una pata (opcional) y una actividad.
// AvanceAcumulado = % acumulado al cierre del día; el estado de la torre en una fecha es el
// último registro hasta esa fecha. NoAplica = la actividad no aplica a esa pata/torre.
public class RegistroAvance
{
    public int Id { get; set; }
    public int InformeDiarioId { get; set; }
    public DateTime Fecha { get; set; }                     // copia de la fecha del informe (consultas)
    public int ElementoTrazadoId { get; set; }
    public int? ComponenteId { get; set; }                  // pata; null = toda la torre
    public int ActividadObraId { get; set; }
    public decimal? Cantidad { get; set; }                  // cantidad del día, en la unidad de la actividad
    public decimal AvanceAcumulado { get; set; }            // 0 a 100
    public bool NoAplica { get; set; }
    public bool Programado { get; set; }                    // programado para el día siguiente (no suma avance)
    public string? Observaciones { get; set; }

    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Registro fotográfico del informe (opcionalmente ligado a un registro).
public class FotoAvance
{
    public int Id { get; set; }
    public int InformeDiarioId { get; set; }
    public int? RegistroAvanceId { get; set; }
    public int? ElementoTrazadoId { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Observacion { get; set; }

    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
