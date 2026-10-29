namespace SigWeb.Data.Papelera;

// Una fila genérica para mostrar en la Papelera, sin importar de qué
// módulo venga (Material, y más adelante Equipos, Inspecciones, etc.)
public class RegistroPapelera
{
    public string Modulo { get; set; } = "";     // Nombre del módulo, ej. "Materiales"
    public int Id { get; set; }
    public string Titulo { get; set; } = "";      // Dato principal, ej. nombre del material
    public string Subtitulo { get; set; } = "";   // Dato secundario, ej. proveedor
    public DateTime? FechaEliminacion { get; set; }
}

// Cada módulo que quiera tener "papelera" implementa esta interfaz una
// sola vez. La página Reciclaje.razor no conoce ningún módulo en
// particular: solo pide la lista de proveedores registrados y los trata
// a todos por igual. Así, agregar papelera a un módulo nuevo NUNCA
// requiere tocar Reciclaje.razor.
public interface IProveedorPapelera
{
    string Modulo { get; }
    List<RegistroPapelera> Listar();
    void Restaurar(int id);
    void EliminarDefinitivo(int id);
    void PurgarVencidos();
}

// Aviso previo a restaurar un registro: qué podría romperse, qué menús se
// verían afectados y cómo resolverlo. Nivel: "info" | "atencion" | "grave".
public class AvisoRestauracion
{
    public string Nivel { get; set; } = "atencion";
    public string Mensaje { get; set; } = "";
    public string Solucion { get; set; } = "";
    public List<string> Menus { get; set; } = new();
}

// Opcional: un proveedor que lo implemente hace que la Papelera muestre sus
// avisos (y pida confirmar) antes de restaurar. Lista vacía = restaurar sin más.
public interface IValidaRestauracion
{
    List<AvisoRestauracion> Revisar(int id);
}
