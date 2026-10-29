namespace SigWeb.Models;

// Un campo de la "plantilla" de un Tipo de Ensayo (ej. para Resistencia
// a la compresión: Diámetro, Carga máx, Área, %Cumplimiento...). Se
// define una sola vez por tipo y sirve tanto para la captura manual
// como para el mapeo al cargar un Excel. Los campos de tipo Fórmula no
// se diligencian: el sistema los calcula solos a partir de los demás.
public class CampoEnsayo
{
    public int Id { get; set; }
    public int TipoEnsayoId { get; set; }

    // Identificador estable usado dentro de las fórmulas de otros campos
    // (ej. "CargaPsi"), sin espacios ni tildes. No cambia aunque el
    // usuario renombre la Etiqueta.
    public string Clave { get; set; } = string.Empty;

    public string Etiqueta { get; set; } = string.Empty;
    public string? Unidad { get; set; }
    public TipoDatoCampoEnsayo TipoDato { get; set; } = TipoDatoCampoEnsayo.Texto;

    // Solo si TipoDato = Formula. Referencia a otros campos del mismo
    // tipo de ensayo por su Clave entre llaves, ej: "{CargaPsi} / {FcDiseno}".
    public string? Formula { get; set; }

    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;

    // Si este campo se puede escribir directamente en la Vista Excel
    // (celda por celda, sin pasar por el diálogo Agregar/Editar). La Vista
    // tabla ya no permite editar nada: toda captura/corrección de datos
    // pasa por aquí. Los campos de tipo Fórmula se calculan solos y nunca
    // son editables sin importar este valor.
    public bool EditableEnVistaExcel { get; set; } = true;

    // Si cada valor de este campo puede llevar un archivo adjunto.
    public bool LlevaAdjunto { get; set; }
}
