namespace SigWeb.Data.Navegacion;

// Ruta (primer tramo de la dirección) → grupo y módulo del menú. Lo usa la ruta de navegación
// del encabezado de página (EncabezadoPagina). Si cambia el menú (NavMenu.razor), cambiar aquí.
public static class MapaNavegacion
{
    public sealed record Modulo(string Grupo, string Nombre, string Href);

    private static readonly Dictionary<string, Modulo> Mapa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["matriz-pie"] = new("Matrices", "Matriz PIE", "matriz-pie"),
        ["avance-obra"] = new("Matrices", "Avance de obra", "avance-obra"),
        ["riesgos"] = new("Matrices", "Riesgos", "riesgos"),
        ["indicadores"] = new("Matrices", "Indicadores", "indicadores"),
        ["planes-accion"] = new("Hallazgos", "Planes de acción", "planes-accion"),
        ["materiales"] = new("Listados", "Materiales", "materiales"),
        ["equipos"] = new("Listados", "Equipos", "equipos"),
        ["planos"] = new("Listados", "Planos", "planos"),
        ["documentos"] = new("Listados", "Documentos", "documentos"),
        ["comunicados"] = new("Listados", "Comunicados", "comunicados"),
        ["ensayos"] = new("Listados", "Ensayos", "ensayos"),
        ["actividades"] = new("Listados", "Actividades", "actividades"),
        ["trazado"] = new("Modelos", "Trazado", "trazado"),
        ["tiros"] = new("Modelos", "Trazado", "trazado"),
        ["ifc"] = new("Modelos", "Modelos 3D", "ifc"),
        ["modelos3d"] = new("Modelos", "Modelos 3D", "ifc"),
        ["linea3d"] = new("Modelos", "Línea 3D", "linea3d"),
        ["dossier"] = new("Utilidades", "Dossier", "dossier"),
        ["catalogos"] = new("Utilidades", "Catálogos", "catalogos"),
        ["temas"] = new("Utilidades", "Temas", "temas"),
        ["papelera"] = new("Utilidades", "Papelera", "papelera"),
    };

    // Dirección relativa (sin el dominio) → módulo, o null (Home, páginas sin grupo).
    public static Modulo? De(string rutaRelativa)
    {
        var primero = rutaRelativa.Split('?', '#')[0].Trim('/').Split('/')[0];
        return Mapa.TryGetValue(primero, out var m) ? m : null;
    }
}
