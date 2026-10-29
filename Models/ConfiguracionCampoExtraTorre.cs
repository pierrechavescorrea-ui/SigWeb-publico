namespace SigWeb.Models;

// Si el usuario quiere OCULTAR un campo adicional de torre (ej. una
// columna que se importó por error, o que ya no interesa mostrar) sin
// borrar los datos ya cargados para ese campo en cada torre. Una fila por
// Clave distinta que haya existido alguna vez en CampoExtraTorre; si una
// Clave no tiene fila aquí todavía, se trata como Activo = true (recién
// importada, se ve por defecto).
public class ConfiguracionCampoExtraTorre
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
