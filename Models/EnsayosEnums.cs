namespace SigWeb.Models;

// Qué tipo de dato es un campo de la plantilla de un Tipo de Ensayo.
public enum TipoDatoCampoEnsayo
{
    Texto = 0,
    Numero = 1,
    Fecha = 2,

    // Se calcula solo a partir de otros campos del mismo resultado
    // (ver CampoEnsayo.Formula); el usuario no lo diligencia.
    Formula = 3,

    // Un documento de referencia compartido por varios resultados (ej.
    // Diseño de mezcla): por ahora es código + adjunto directo, hasta
    // que exista el Listado Maestro de documentos externos.
    DocumentoUnico = 4
}

// A qué parte de la pata corresponde un resultado (cuando aplica). Una
// muestra puede ser del vaciado general de la pata (Ninguno), o
// específica del Pedestal o la Zapata; Otro es para elementos que no
// están ligados a ninguna pata (ej. "CUNETA").
public enum SubElementoResultado
{
    Ninguno = 0,
    Pedestal = 1,
    Zapata = 2,
    Otro = 3
}
