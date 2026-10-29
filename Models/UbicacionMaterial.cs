namespace SigWeb.Models;

// Dónde se usó un material (trazabilidad): una torre, un elemento/pata de una torre o un tiro.
// Un material puede tener varias. Con ComponenteId también se guarda la torre (ElementoTrazadoId).
// Quitar una ubicación es borrado directo (es una relación, no un registro con historia).
public class UbicacionMaterial
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public int? ElementoTrazadoId { get; set; }   // torre
    public int? ComponenteId { get; set; }        // elemento / pata de esa torre
    public int? TiroId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
}
