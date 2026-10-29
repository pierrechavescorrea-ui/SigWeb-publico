namespace SigWeb.Models;

// Asignación manual en Modelos 3D: a una pieza del IFC (Pset SigWeb_Pieza.Id) se le asigna una fila de
// la lista de materiales (hoja + marca → perfil L) para que se dibuje con su geometría real en vez del
// tubo de "perfil no leído", y/o un archivo del listado maestro de planos que abre su ficha.
// Modelo = nombre del archivo sin extensión (torre-aae1), el mismo para el .ifc y el .json.
public class AsignacionPiezaModelo
{
    public int Id { get; set; }
    public string Modelo { get; set; } = string.Empty;
    public string Pieza { get; set; } = string.Empty;
    public int? Hoja { get; set; }
    public string? Marca { get; set; }
    public string? Perfil { get; set; }
    public int? ArchivoPlanoMaestroId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
}
