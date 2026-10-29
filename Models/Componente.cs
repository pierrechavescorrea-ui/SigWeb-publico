namespace SigWeb.Models;

// Un elemento "hijo" lógico de una torre: sus patas (A, B, C, D, creadas
// automáticamente al importar cada torre nueva) y cualquier elemento
// adicional que el usuario agregue manualmente (trinchos, cunetas,
// gaviones, obras de protección, etc.). No tiene coordenadas propias:
// no se dibuja aparte en el mapa, vive "dentro" de su torre.
public class Componente
{
    public int Id { get; set; }
    public int ElementoTrazadoId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // true para las 4 patas creadas automáticamente; false para lo que
    // el usuario agrega manualmente después.
    public bool EsPredeterminado { get; set; } = false;

    // true solo para el ítem especial "Hoja de vida" que toda torre tiene
    // obligatoriamente (uno solo, no se puede renombrar ni eliminar, y
    // siempre muestra TODAS las actividades del catálogo). Los demás
    // elementos (patas, trinchos, gaviones...) pueden limitarse a solo
    // algunas actividades vía ComponenteActividad.
    public bool EsHojaDeVida { get; set; } = false;

    public int Orden { get; set; } = 0;

    // Coordenadas reales, opcionales: cuando el KMZ trae el punto exacto
    // de esta pata (no solo el eje de la torre), se guardan aquí para
    // poder dibujarla en el mapa si el usuario activa esa vista. Si son
    // null, la pata sigue existiendo igual, solo que no tiene ubicación
    // propia (vive "dentro" de su torre, como antes).
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }

    // ---------- Apariencia en el mapa (solo aplica a patas con
    // Latitud/Longitud o a elementos con GeometriaJson) ----------
    // Override propio de este elemento; si es null, se usa el valor por
    // defecto configurado para "patas" en Trazado.
    public string? IconoMapa { get; set; }
    public string? ColorMapa { get; set; }

    // Palomita "ver en el mapa": permite ocultar un elemento puntual sin
    // eliminarlo (por ejemplo, una pata sin ubicación confiable o una
    // cuneta que todavía no se quiere mostrar).
    public bool MostrarEnMapa { get; set; } = true;

    // ---------- Importado desde DWG/DXF (cunetas, trinchos, obras de
    // protección cercanas a la torre) ----------
    // Lista de puntos [[lat,lon], [lat,lon], ...] en JSON, ya convertidos a
    // coordenadas reales (ver ImportadorCad), para dibujar una polilínea en
    // el mapa junto a la torre. Si es null, el componente no tiene geometría
    // propia (como las patas normales).
    public string? GeometriaJson { get; set; }

    // Texto armado con la información (textos, anotaciones, tablas de
    // cantidades...) que el usuario eligió conservar del plano al importar,
    // para detallar características del elemento (p. ej. área de terraza).
    public string? InformacionAdicional { get; set; }

    // Ruta del DWG/DXF original del que se importó este elemento, por si se
    // quiere volver a consultar el plano completo.
    public string? OrigenArchivoDwg { get; set; }

    // Ruta de la miniatura (PNG) extraída del DWG, para mostrarla junto al
    // elemento sin tener que abrir el plano.
    public string? MiniaturaDwg { get; set; }

    // ---------- Papelera de reciclaje ----------
    // Igual que Material: "eliminar" no borra de la base de datos, solo
    // marca Eliminado = true. Queda disponible en Papelera hasta 30 días.
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}