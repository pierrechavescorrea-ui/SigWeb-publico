namespace SigWeb.Data.Ubicacion;

// Una fila genérica para el panel lateral del Trazado: "en la torre/pata X
// hay este registro de este módulo". Igual espíritu que RegistroPapelera:
// la página del mapa no necesita conocer los módulos, solo pregunta.
public class RegistroUbicacion
{
    public string Modulo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Detalle { get; set; } = "";
}

// Cada módulo que permita vincular sus registros a una pata (Componente)
// del trazado (Material hoy; Ensayos, Equipos, No Conformidades más
// adelante) implementa esta interfaz una vez y la registra en Program.cs.
// Trazado.razor pide "qué hay en esta pata" a todos los proveedores registrados.
public interface IProveedorUbicacion
{
    string Modulo { get; }
    List<RegistroUbicacion> ObtenerPara(int componenteId);
}