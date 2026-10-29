namespace SigWeb.Data.Iconos;

// Mapa de la app para el selector "Usar en…" de Catálogos: por cada opción del menú, su ícono,
// el ícono del encabezado de su pantalla y los botones estándar (Ico) que usa. Generado a partir
// de NavMenu.razor y de las páginas; si se agrega un módulo, se agrega aquí.
public record ModuloIconos(string Nombre, string Ruta, string MenuSvg, string? CabClave, IReadOnlyList<string> Conceptos)
{
    public string MenuPunto => "menu:" + Ruta;
}

public static class PuntosIcono
{
    public static readonly IReadOnlyList<ModuloIconos> Modulos = new List<ModuloIconos>
    {
        new("Materiales", "materiales", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M12 3l9 4.5v9L12 21l-9-4.5v-9z""/><path d=""M3 7.5l9 4.5 9-4.5M12 12v9""/></svg>", "materiales", new[] { "agregar", "cancelar", "editar", "excel", "pdf", "importar", "exportar", "cargar-excel", "cargas", "pantalla-completa", "ubicacion" }),
        new("Catálogos", "catalogos", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M4 5a2 2 0 012-2h13v16H6a2 2 0 00-2 2z""/><path d=""M4 19V5M9 7h6""/></svg>", "catalogos", new[] { "agregar", "cancelar", "catalogos", "editar", "eliminar", "iconos", "usar-en" }),
        new("Papelera", "papelera", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6""/></svg>", "papelera", new[] { "cancelar", "eliminar", "eliminar-definitivo", "restaurar" }),
        new("Trazado", "trazado", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M12 21s-7-6.2-7-11a7 7 0 0114 0c0 4.800-7 11-7 11z""/><circle cx=""12"" cy=""10"" r=""2.500""/></svg>", "trazado", new[] { "agregar", "cancelar", "editar", "eliminar", "guardar", "informacion", "riesgos", "plan-accion", "carpeta", "importar", "exportar", "mapa", "listado", "capas", "unificar", "tabla", "inspeccionar-cad", "importar-cad" }),
        new("Planos", "planos", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M4 4h16v16H4z""/><path d=""M4 10h16M10 4v16""/></svg>", "planos", new[] { "agregar", "cancelar", "cargar-excel", "cargar-pdf", "editar", "eliminar", "exportar", "cargas", "nueva-version", "documento", "listado", "versiones", "pantalla-completa" }),
        new("Documentos", "documentos", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M6 3h8l4 4v14H6z""/><path d=""M14 3v4h4M9 12h6M9 16h6""/></svg>", "documentos", new[] { "agregar", "cancelar", "editar", "eliminar", "exportar", "guardar", "importar", "nueva-version", "cargas", "filtro", "documento", "adjuntar", "cargar-excel", "cargar-pdf", "listado", "versiones", "pantalla-completa" }),
        new("Comunicados", "comunicados", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><rect x=""3"" y=""5"" width=""18"" height=""14"" rx=""2""/><path d=""m3.5 6.5 8.5 6 8.5-6""/></svg>", "comunicados", new[] { "agregar", "cancelar", "editar", "eliminar", "exportar", "guardar", "cargas", "cargar-excel", "documento", "adjuntar", "nube", "enlace", "grafico", "sugerencia", "aceptar", "corregir", "listado", "pantalla-completa" }),
        new("Matriz PIE", "matriz-pie", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M4 4h16v16H4z""/><path d=""M4 9.300h16M4 14.700h16M9.300 4v16M14.700 4v16""/></svg>", "matriz", new[] { "agregar", "cancelar", "editar", "eliminar", "pata", "tiro", "torre" }),
        new("Avance de obra", "avance-obra", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M3 21h18""/><path d=""M7.5 21 11 3h2l3.5 18""/><path d=""M9.2 14h5.6M10.4 8.5h3.2""/><path d=""m8.3 21 7.1-7M15.7 21l-7.1-7""/></svg>", "avance-obra", new[] { "agregar", "cancelar", "eliminar", "guardar", "exportar", "tablero", "torre", "pata", "listado", "documento", "catalogos", "filtro", "restaurar" }),
        new("Actividades", "actividades", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M9 6h11M9 12h11M9 18h11""/><path d=""M4 6l1 1 2-2M4 12l1 1 2-2M4 18l1 1 2-2""/></svg>", "actividades", new[] { "agregar", "cancelar", "editar", "eliminar", "guardar", "pata", "tiro", "torre" }),
        new("Ensayos", "ensayos", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M9 3h6M10 3v6l-5 10a2 2 0 002 3h10a2 2 0 002-3l-5-10V3""/><path d=""M8 15h8""/></svg>", "ensayos", new[] { "agregar", "cancelar", "editar", "eliminar", "importar", "exportar" }),
        new("Equipos", "equipos", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M14.500 6.500a4 4 0 01-5 5L4 17l3 3 5.500-5.500a4 4 0 005-5l-2.500 2.500-2-2z""/></svg>", "equipos", new[] { "agregar", "calibracion", "cancelar", "editar", "eliminar", "guardar", "mantenimiento", "importar", "exportar", "cargar-excel", "cargas", "pantalla-completa" }),
        new("Línea 3D", "linea3d", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M3 20h18M7 20 9 6l2 14M13 20l2-14 2 14""/><path d=""M4 9c3 2 6 2 8 0s5-2 8 0""/></svg>", "linea3d", System.Array.Empty<string>()),
        new("Modelos 3D", "ifc", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M12 2.5 4 7v9l8 4.5 8-4.5V7z""/><path d=""M4 7l8 4.5L20 7M12 11.5v9""/><path d=""M15.5 13.5l2.5-1.4M15.5 16l2.5-1.4""/></svg>", "ifc", new[] { "cancelar", "listado", "ubicar" }),
        new("Indicadores", "indicadores", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M3 3v18h18""/><path d=""m7 15 4-4 3 3 5-6""/><path d=""M19 8V5h-3""/></svg>", "indicadores", new[] { "agregar", "cancelar", "editar", "eliminar", "guardar", "grafico", "indicador", "tablero", "listado", "informacion", "candado", "candado-abierto", "adjuntar", "documento", "pantalla-completa" }),
        new("Riesgos", "riesgos", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M10.3 3.9 1.9 18.4A2 2 0 0 0 3.6 21.4h16.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z""/><path d=""M12 9v4.5M12 17.2v.1""/></svg>", "riesgos", new[] { "agregar", "cancelar", "editar", "eliminar", "exportar", "guardar", "listado", "riesgos", "seguimiento", "versiones", "plan-accion", "subir", "bajar", "adjuntar", "importar" }),
        new("Planes de acción", "planes-accion", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><rect x=""5"" y=""3.5"" width=""14"" height=""18"" rx=""2""/><path d=""M9 2.5h6v3H9z""/><path d=""m8.5 11.5 1.6 1.6 3.2-3.2M8.5 17h7""/></svg>", "plan-accion", new[] { "agregar", "cancelar", "eliminar", "exportar", "guardar", "plan-accion", "subir", "bajar", "riesgos", "adjuntar" }),
        new("Dossier", "dossier", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M3.5 4h17v4h-17z""/><path d=""M5 8v12h14V8""/><path d=""M10 12h4""/></svg>", "dossier", new[] { "comprimir", "excel", "documento", "versiones", "carpeta", "listado" }),
        new("Temas", "temas", @"<svg class=""nav-ico"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><circle cx=""12"" cy=""12"" r=""9""/><circle cx=""8.5"" cy=""10"" r=""1.3""/><circle cx=""12"" cy=""7.5"" r=""1.3""/><circle cx=""15.5"" cy=""10"" r=""1.3""/><path d=""M12 21a2.5 2.5 0 0 1 0-5h2a3 3 0 0 0 3-3""/></svg>", "temas", System.Array.Empty<string>()),
    };

    // Todos los botones estándar (conceptos de Ico) con su nombre.
    public static readonly IReadOnlyDictionary<string, string> Conceptos = new Dictionary<string, string>
    {
        ["excel"] = "Excel",
        ["pdf"] = "PDF",
        ["importar"] = "Importar",
        ["cargar-excel"] = "Importar Excel",
        ["cargar-pdf"] = "Importar PDF",
        ["exportar-excel"] = "Exportar a Excel",
        ["exportar-pdf"] = "Exportar a PDF",
        ["editar"] = "Editar",
        ["guardar"] = "Guardar",
        ["eliminar"] = "Eliminar",
        ["cancelar"] = "Cancelar",
        ["calibracion"] = "Calibración",
        ["mantenimiento"] = "Mantenimiento",
        ["listado"] = "Listado",
        ["iconos"] = "Íconos",
        ["restaurar"] = "Restaurar",
        ["eliminar-definitivo"] = "Eliminar definitivamente",
        ["agregar"] = "Agregar",
        ["usar-en"] = "Usar ícono en…",
        ["catalogos"] = "Catálogos",
        ["torre"] = "Torre",
        ["pata"] = "Pata",
        ["tiro"] = "Tiro (tendido)",
        ["ubicar"] = "Ver en el modelo",
        ["ubicacion"] = "Ubicación",
        ["exportar"] = "Exportar",
        ["activar"] = "Activar / desactivar",
        ["pantalla-completa"] = "Pantalla completa",
        ["riesgos"] = "Riesgos",
        ["seguimiento"] = "Seguimiento",
        ["versiones"] = "Revisiones",
        ["plan-accion"] = "Plan de acción",
        ["subir"] = "Subir",
        ["adjuntar"] = "Adjuntar",
        ["informacion"] = "Información",
        ["bajar"] = "Bajar",
        ["carpeta"] = "Archivos",
        ["nueva-version"] = "Nueva versión",
        ["cargas"] = "Cargas",
        ["filtro"] = "Filtrar",
        ["documento"] = "Documento",
        ["nube"] = "Nube (enlace)",
        ["enlace"] = "Enlace",
        ["grafico"] = "Gráficos",
        ["sugerencia"] = "Respuesta sugerida",
        ["aceptar"] = "Aceptar",
        ["corregir"] = "Corregir",
        ["candado"] = "Congelar",
        ["indicador"] = "Indicador por indicador",
        ["tablero"] = "Tablero (todos)",
        ["candado-abierto"] = "Descongelar",
        ["mapa"] = "Mapa",
        ["tabla"] = "Tabla de torres",
        ["capas"] = "Base de datos (capas)",
        ["unificar"] = "Unificar duplicados",
        ["importar-cad"] = "Importar desde DWG/DXF",
        ["inspeccionar-cad"] = "Inspeccionar DWG/DXF",
        ["comprimir"] = "Descargar dossier (ZIP)",
    };

    public static string NombreConcepto(string c) => Conceptos.TryGetValue(c, out var n) ? n : c;
}
