namespace SigWeb.Models;

// Una sola fila en esta tabla: la configuración general de la empresa.
// Por ahora solo guarda el logo, pero aquí es donde más adelante
// podrían ir otros datos institucionales (nombre legal, NIT, etc.).
public class ConfiguracionEmpresa
{
    public int Id { get; set; }
    public string? LogoUrl { get; set; }

    // ---------- Preferencias del mapa de Trazado ----------
    // Forma del ícono de las torres: "torre" (silueta de torre), "pin",
    // "circulo", "cuadrado", "diamante", "triangulo" o "estrella".
    // Los nombres se mantienen en español porque así los usa también
    // wwwroot/js/mapaTrazado.js (crearIcono).
    public string IconoTorre { get; set; } = "torre";

    // Color (hex) con el que se dibuja el ícono de las torres.
    public string ColorTorre { get; set; } = "#EC1C24";

    // Forma del ícono de las patas cuando se muestran en el mapa:
    // "pedestal" (pedestal de concreto con stub metálico embebido, el
    // caso típico de una pata de torre), o cualquiera de las mismas
    // formas genéricas que IconoTorre.
    public string IconoPata { get; set; } = "pedestal";

    // Color (hex) con el que se dibuja el ícono de las patas.
    public string ColorPata { get; set; } = "#666666";

    // Capa base del mapa: "calle" (OpenStreetMap), "satelite" (imágenes
    // satelitales), "hibrido" (satélite + nombres de lugares) o
    // "terreno" (relieve). El usuario también puede cambiarla al vuelo
    // desde el selector de capas del propio mapa (como en Google Earth);
    // este valor es solo la que se muestra al abrir.
    public string CapaBaseMapa { get; set; } = "calle";

    // Si además del eje de la torre se dibujan también sus patas (cuando
    // tienen coordenadas reales tomadas del KMZ). El usuario decide: por
    // defecto solo se ve el eje, y al hacer clic aparecen las patas en el
    // panel lateral (no en el mapa), para no saturar la vista.
    public bool MostrarPatasEnMapa { get; set; } = false;

    // Orden de las pestañas de la ficha (hoja de vida y elementos) en todas las torres, arrastrando:
    // claves separadas por coma ("archivos", "informacion", "riesgos", "planes", "a{IdActividad}").
    // Null = orden de siempre.
    public string? OrdenPestanasFicha { get; set; }

    // ---------- Listado de comunicados ----------
    // Días para responder: hasta este número de días la respuesta va "A tiempo"; después, "Vencida".
    // Quién debe responder según el sentido (los de salida los responde la otra parte).
    // Null = los valores del Excel de control (8 días, salidas → INGETEC, entradas → JEJ): así la fila
    // de configuración que ya existe no queda con 0 días ni responsables vacíos tras la migración.
    public int? ComunicadosPlazoDias { get; set; }
    public string? ComunicadosRespondeSalida { get; set; }
    public string? ComunicadosRespondeEntrada { get; set; }

    // ---------- Inicio (tablero) ----------
    // Responsable cuyos comunicados vencidos muestra el tablero de inicio. Null = quien responde las entradas (JEJ);
    // "*" = todos los responsables.
    public string? TableroResponsableComunicados { get; set; }

    // ---------- Avance de obra ----------
    // Qué dato de la tabla de torres da cada cota del visor 3D (JSON de CotasAvanceObra). Null = sin configurar.
    public string? AvanceObraCotas { get; set; }
}