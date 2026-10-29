namespace SigWeb.Models;

// Une un ResultadoEnsayo con cada pata (Componente) que cubre. La
// mayoría de los casos es una sola pata, pero una misma muestra puede
// cubrir varias (ej. "BC" en la columna Elemento = patas B y C
// comparten el mismo resultado, informe y demás datos).
public class ResultadoEnsayoComponente
{
    public int Id { get; set; }
    public int ResultadoEnsayoId { get; set; }
    public int ComponenteId { get; set; }
}
