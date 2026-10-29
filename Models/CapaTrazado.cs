namespace SigWeb.Models;

// Una "capa" agrupa las torres que llegaron juntas en una misma carga de
// KMZ o Excel (o las que ya existían antes de que existiera este
// concepto). Sirve para poder identificar qué se cargó y cuándo, apagar
// su visibilidad en el mapa sin borrar nada, renombrarla, o enviar sus
// torres a la Papelera de una sola vez.
public class CapaTrazado
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    // "Kmz", "Excel" o "Inicial" (torres que ya existían antes de este
    // concepto de capas). Solo informativo, no cambia el comportamiento.
    public string Origen { get; set; } = string.Empty;

    public DateTime FechaCarga { get; set; } = DateTime.Now;

    // Referencia/versión que le da el usuario a esta carga (ej. "A29-9"),
    // para identificar con qué versión del KMZ/tabla del topógrafo quedó
    // cada dato. Opcional: si no se llena, la capa se identifica solo por
    // su fecha, como antes.
    public string? Version { get; set; }

    // Si está apagada, sus torres no se dibujan en el mapa (pero siguen
    // existiendo normalmente: buscador, ficha, papelera, todo sigue
    // funcionando igual, solo no aparecen en el mapa).
    public bool Visible { get; set; } = true;
}