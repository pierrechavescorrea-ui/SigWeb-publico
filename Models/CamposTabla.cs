namespace SigWeb.Models;

// ---------- "Campos" de cualquier tabla/lista de la app ----------
// Cada tabla se identifica con una clave de texto (ej. "planos"). Sus
// columnas propias (las del modelo) se registran solas como campos
// Nativos; las que llegan de un Excel quedan como Excel, y las que el
// usuario crea, como Manual. Todas se pueden ocultar, reordenar y llevar
// formatos condicionales; las Manual/Excel guardan su valor por fila en
// ValorCampoTabla y pueden ser digitadas o calculadas (fórmula tipo Excel).

public enum OrigenCampoTabla { Nativo = 0, Excel = 1, Manual = 2 }

// Auto: el tipo lo da el resultado de cada celda (para fórmulas que devuelven texto o número según el caso).
public enum TipoCampoTabla { Texto = 0, Numero = 1, Fecha = 2, Lista = 3, SiNo = 4, Auto = 5 }

public class CampoTabla
{
    public int Id { get; set; }
    public string Tabla { get; set; } = string.Empty;
    // Nativo: nombre de la propiedad (ej. "Codigo"). Excel/Manual: generada al crear.
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public OrigenCampoTabla Origen { get; set; } = OrigenCampoTabla.Manual;
    public TipoCampoTabla Tipo { get; set; } = TipoCampoTabla.Texto;
    // Calculado = su valor sale de Formula (para toda la columna); si no, se digita.
    public bool Calculado { get; set; }
    public string? Formula { get; set; }
    // Lista: opciones de un catálogo (ItemCatalogo.Tipo) o, si es null, de Opciones (una por línea).
    public string? CatalogoTipo { get; set; }
    public string? Opciones { get; set; }
    public int? Decimales { get; set; }
    // El usuario cambió el tipo de una columna propia en el panel Campos: ya no se toma el que declara la página.
    public bool TipoFijadoPorUsuario { get; set; }
    public bool Visible { get; set; } = true;
    public int Orden { get; set; }

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Valor de un campo Excel/Manual en una fila. Formula (si existe) manda sobre
// Valor solo en esta celda: es la opción "aplicar solo a esta celda".
public class ValorCampoTabla
{
    public int Id { get; set; }
    public int CampoTablaId { get; set; }
    public int FilaId { get; set; }
    public string? Valor { get; set; }
    public string? Formula { get; set; }
}

public enum OperadorFormato
{
    Igual = 0, Diferente = 1, Mayor = 2, MayorIgual = 3, Menor = 4, MenorIgual = 5, Entre = 6, NoEntre = 7,
    Contiene = 8, NoContiene = 9, EmpiezaCon = 10, TerminaCon = 11, Vacio = 12, NoVacio = 13,
    FechaAntesDeHoy = 14, FechaHoy = 15, FechaDespuesDeHoy = 16, FechaProximosDias = 17, FechaUltimosDias = 18,
    Formula = 19,
    // Valor repetido / único en la columna (como "Duplicar valores" de Excel) y códigos con espacios alrededor de guiones.
    Duplicado = 20, Unico = 21, CodigoConEspacios = 22
}

// Regla de formato condicional de un campo (la primera que se cumple, en orden, es la que se aplica).
public class FormatoCondicional
{
    public int Id { get; set; }
    public int CampoTablaId { get; set; }
    public OperadorFormato Operador { get; set; }
    public string? Valor1 { get; set; }
    public string? Valor2 { get; set; }
    // Estilo predefinido (verde, rojo, amarillo…) o "personalizado".
    public string Estilo { get; set; } = "verde";
    public string? ColorTexto { get; set; }
    public string? ColorFondo { get; set; }
    public bool Negrita { get; set; } = true;
    // "pildora" (como la imagen de referencia), "celda" (toda la celda) o "texto" (solo el color del texto).
    public string Forma { get; set; } = "pildora";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
