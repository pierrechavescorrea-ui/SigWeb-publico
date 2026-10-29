using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.AvanceObra;

// Actividades de obra de fábrica. Estructura y pesos: hoja "Avance acumulado" del formato GEB
// ENL-DIP-PRO-021-F-054 (informe del 28/10/2026), fila 1 "PESOS". Ítems contractuales: hoja
// "Reporte Diario", capítulo 2.1 ZAPATA (las parrillas usan 2.2/2.3: se ajusta en Ajustes).
// Tendido (0,28) y preliminares (0,07) no se siembran: van por tiro / fuera del alcance de la Fase 1.
// Solo corre si la tabla está vacía (incluida la Papelera); después todo es editable.
public static class SembradorActividadesObra
{
    public static void SembrarSiVacio(AppDbContext db)
    {
        if (db.ActividadesObra.IgnoreQueryFilters().Any()) return;
        int o = 0;
        ActividadObra A(string nombre, string grupo, AmbitoAvance amb, decimal peso, string? unidad, string? item, EfectoAvance3D ef) =>
            new() { Nombre = nombre, Grupo = grupo, Ambito = amb, Peso = peso, Unidad = unidad, ItemContrato = item, Efecto = ef, Orden = ++o };
        db.ActividadesObra.AddRange(
            A("Excavación", "Obra civil", AmbitoAvance.Pata, 0.059m, "m³", "2.1.1", EfectoAvance3D.Excavacion),
            A("Solado", "Obra civil", AmbitoAvance.Pata, 0.007m, "m³", "2.1.5", EfectoAvance3D.Solado),
            A("Nivelación de cimentación", "Obra civil", AmbitoAvance.Pata, 0.043m, null, null, EfectoAvance3D.Nivelacion),
            A("Acero de refuerzo", "Obra civil", AmbitoAvance.Pata, 0.033m, "kg", "2.1.4", EfectoAvance3D.Acero),
            A("Concreto", "Obra civil", AmbitoAvance.Pata, 0.081m, "m³", "2.1.2", EfectoAvance3D.Concreto),
            A("Relleno y compactación", "Obra civil", AmbitoAvance.Pata, 0.037m, "m³", "2.1.3", EfectoAvance3D.Relleno),
            A("Sistema de puesta a tierra", "SPT", AmbitoAvance.Torre, 0.06m, "Sitio de torre", null, EfectoAvance3D.Ninguno),
            A("Prearmado", "Montaje", AmbitoAvance.Torre, 0m, "t", null, EfectoAvance3D.Prearmado),
            A("Montaje", "Montaje", AmbitoAvance.Torre, 0.13m, "t", null, EfectoAvance3D.Montaje),
            A("Verticalidad, torsión y torque", "Montaje", AmbitoAvance.Torre, 0.06m, null, null, EfectoAvance3D.Verticalidad),
            A("Revisión y ajuste de estructura", "Montaje", AmbitoAvance.Torre, 0m, null, null, EfectoAvance3D.Ninguno),
            A("Señalización", "Montaje", AmbitoAvance.Torre, 0m, null, null, EfectoAvance3D.Ninguno),
            A("Medición de resistencia de puesta a tierra", "Montaje", AmbitoAvance.Torre, 0m, null, null, EfectoAvance3D.Ninguno),
            A("Vestida", "Montaje", AmbitoAvance.Torre, 0.11m, "t", null, EfectoAvance3D.Vestida));
        db.SaveChanges();
    }
}
