namespace SigWeb.Models;

// Un dato adicional de una torre, cargado desde la "tabla de torres" en
// Excel (tipo de cimentación, SPT, observaciones, etc.). Se guarda como
// Clave/Valor en vez de columnas fijas porque esa tabla trae decenas de
// campos distintos según el proyecto, y el usuario elige cuáles migrar
// al importar — así nunca hace falta una migración nueva para agregar
// un campo que antes no se había usado.
public class CampoExtraTorre
{
    public int Id { get; set; }
    public int ElementoTrazadoId { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;

    // De qué carga vino este valor (nombre del archivo + fecha/hora, y la
    // "referencia de versión" que haya escrito el usuario al subirlo), para
    // saber con qué versión quedó cada dato cuando la tabla de torres del
    // proyecto va cambiando. Se sobrescribe cada vez que el campo se
    // vuelve a importar.
    public string Origen { get; set; } = string.Empty;

    // Igual que en Ensayos/Componentes: "eliminar" solo marca esto (va a la
    // Papelera, recuperable 30 días) en vez de borrar de una — tanto al
    // quitar un dato suelto como al eliminar una carga completa desde
    // "Tabla de torres".
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
