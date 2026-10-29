namespace SigWeb.Models;

// Qué ícono del catálogo se usa en un punto concreto de la app (en vez del ícono por defecto).
// Punto: "menu:{ruta}" (opción del menú lateral), "cab:{clave}" (ícono del encabezado de una
// pantalla) o "ico:{concepto}" (botón estándar: editar, eliminar, guardar…, en toda la app).
public class AsignacionIcono
{
    public int Id { get; set; }
    public string Punto { get; set; } = string.Empty;
    public int IconoCatalogoId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
}
