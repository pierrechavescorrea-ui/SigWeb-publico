namespace SigWeb.Models;

// Un estado de avance de un parámetro del PIE (ej. "En revisión
// Interventoría", "Aprobado"). Lo crea el usuario y le asigna el peso (%)
// que aporta al cumplimiento. El % NO se copia a los registros: cada
// RegistroParametro solo apunta al estado, así que al cambiar el peso
// aquí se recalcula todo automáticamente.
public class EstadoParametro
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    // 0 a 100
    public decimal Porcentaje { get; set; } = 0;
    public string Color { get; set; } = "#9e9e9e";
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;
}
