namespace SigWeb.Models;

public class ItemCatalogo
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    // ---------- Formato con que se muestra el valor en las listas ----------
    // Igual que los formatos condicionales de "Campos": un estilo de la
    // paleta ("verde", "rojo-suave"…), "personalizado" (usa los colores) o
    // null (sin formato). Forma: "pildora", "celda" o "texto".
    public string? Estilo { get; set; }
    public string? ColorTexto { get; set; }
    public string? ColorFondo { get; set; }
    public string Forma { get; set; } = "pildora";
    public bool Negrita { get; set; } = true;

    // ---------- Vínculo con el trazado (KMZ/Excel) ----------
    // Solo aplica a Tipo == "Localización": a qué torre del trazado
    // pertenece este ítem del catálogo. Si esa torre se renombra o se
    // migra (misma torre, nuevo nombre en un nuevo KMZ/Excel), este ítem
    // se actualiza automáticamente en vez de duplicarse. Null si el ítem
    // fue creado manualmente y no proviene del trazado.
    public int? ElementoTrazadoId { get; set; }
}
