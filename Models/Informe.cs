namespace SigWeb.Models;

// Un informe de laboratorio, propio de un Tipo de Ensayo (cada tipo
// numera los suyos: el informe 1 de Resistencia a la compresión no
// tiene relación con el informe 1 de Proctor). Varios resultados
// pueden compartir el mismo informe, y un informe puede traer varios
// PDF (el principal, anexos, certificados de calibración, etc.).
public class Informe
{
    public int Id { get; set; }
    public int TipoEnsayoId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime? Fecha { get; set; }
}
