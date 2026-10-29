namespace SigWeb.Models;

public class ValorCampoEquipo
{
    public int Id { get; set; }
    public int EquipoId { get; set; }
    public int CampoEquipoId { get; set; }
    public string Valor { get; set; } = string.Empty;
}
