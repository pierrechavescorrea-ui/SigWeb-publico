namespace SigWeb.Models;

// Vínculo entre un componente (pata, trincho, gavión...) y UN archivo
// puntual del listado maestro de planos (no toda la fila/código: un
// mismo código puede agrupar varias hojas — H25, H26...— y el componente
// solo necesita la que le corresponde a él). Muchos a muchos: un mismo
// archivo (ej. un plano tipo de trincho) puede estar vinculado a decenas
// de componentes sin duplicarse, y un componente puede tener más de un
// archivo vinculado (ej. cimentación + puesta a tierra).
public class ComponentePlano
{
    public int Id { get; set; }
    public int ComponenteId { get; set; }
    public int ArchivoPlanoMaestroId { get; set; }
    public DateTime FechaVinculo { get; set; } = DateTime.Now;
}
