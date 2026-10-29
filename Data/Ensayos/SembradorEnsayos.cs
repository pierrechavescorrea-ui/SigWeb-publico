using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Ensayos;

// Siembra los dos grupos de ejemplo que el usuario pidió explícitamente
// (Concretos, Suelos) con sus tipos de ensayo típicos. Es solo un punto
// de partida: todo se puede renombrar, reordenar, desactivar o ampliar
// después desde el módulo Ensayos. Debe correr después de
// SembradorIconosCatalogo (usa sus íconos por Clave). Seguro de llamar
// en cada arranque: si un grupo ya existe (por Nombre), no lo duplica,
// y solo le agrega los tipos de ensayo (y campos) que todavía no tenga.
public static class SembradorEnsayos
{
    public static void SembrarSiHaceFalta(AppDbContext db)
    {
        var iconos = db.IconosCatalogo.ToDictionary(i => i.Clave, i => i.Id, StringComparer.OrdinalIgnoreCase);
        int? IconoId(string clave) => iconos.TryGetValue(clave, out var id) ? id : null;

        var gruposExistentes = db.GruposEnsayo.IgnoreQueryFilters().ToDictionary(g => g.Nombre, StringComparer.OrdinalIgnoreCase);

        GrupoEnsayo ObtenerOCrearGrupo(string nombre, string claveIcono, int orden)
        {
            if (gruposExistentes.TryGetValue(nombre, out var existente)) return existente;

            var nuevo = new GrupoEnsayo { Nombre = nombre, IconoCatalogoId = IconoId(claveIcono), Orden = orden };
            db.GruposEnsayo.Add(nuevo);
            db.SaveChanges();
            gruposExistentes[nombre] = nuevo;
            return nuevo;
        }

        TipoEnsayo ObtenerOCrearTipo(GrupoEnsayo grupo, string nombre, string? claveIcono, int orden)
        {
            var existente = db.TiposEnsayo.IgnoreQueryFilters().FirstOrDefault(t => t.GrupoEnsayoId == grupo.Id && t.Nombre == nombre);
            if (existente is not null) return existente;

            var nuevo = new TipoEnsayo
            {
                GrupoEnsayoId = grupo.Id,
                Nombre = nombre,
                IconoCatalogoId = claveIcono is null ? null : IconoId(claveIcono),
                Orden = orden
            };
            db.TiposEnsayo.Add(nuevo);
            db.SaveChanges();
            return nuevo;
        }

        var concretos = ObtenerOCrearGrupo("Concretos", "cilindro-concreto", 1);
        var compresion = ObtenerOCrearTipo(concretos, "Resistencia a la compresión", null, 1); // usa el ícono del grupo
        ObtenerOCrearTipo(concretos, "Slump", "slump", 2);

        var suelos = ObtenerOCrearGrupo("Suelos", "cono-arena", 2);
        ObtenerOCrearTipo(suelos, "Peso unitario o densidades", null, 1); // usa el ícono del grupo
        ObtenerOCrearTipo(suelos, "Proctor", "proctor", 2);
        ObtenerOCrearTipo(suelos, "PDC", "dcp", 3);

        SembrarCamposCompresion(db, compresion.Id);

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Plantilla de campos de "Resistencia a la compresión", a partir del
    // Excel de ejemplo que entregó el usuario. Deja listo lo que casi
    // siempre se necesita para ese ensayo; el usuario puede agregar,
    // renombrar o quitar campos después desde "Configurar campos".
    private static void SembrarCamposCompresion(AppDbContext db, int tipoEnsayoId)
    {
        var yaExisten = db.CamposEnsayo.Where(c => c.TipoEnsayoId == tipoEnsayoId)
            .Select(c => c.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);

        void Campo(string clave, string etiqueta, TipoDatoCampoEnsayo tipo, string? unidad, string? formula, int orden)
        {
            if (yaExisten.Contains(clave)) return;
            db.CamposEnsayo.Add(new CampoEnsayo
            {
                TipoEnsayoId = tipoEnsayoId,
                Clave = clave,
                Etiqueta = etiqueta,
                TipoDato = tipo,
                Unidad = unidad,
                Formula = formula,
                Orden = orden
            });
        }

        Campo("NoMuestra", "No. Muestra", TipoDatoCampoEnsayo.Numero, null, null, 1);
        Campo("NoCilindro", "No. Cilindro", TipoDatoCampoEnsayo.Numero, null, null, 2);
        Campo("FechaMuestra", "Fecha muestra", TipoDatoCampoEnsayo.Fecha, null, null, 3);
        Campo("FechaEnsayo", "Fecha ensayo", TipoDatoCampoEnsayo.Fecha, null, null, 4);
        Campo("Edad", "Edad", TipoDatoCampoEnsayo.Numero, "días", null, 5);
        Campo("Diametro", "Diámetro", TipoDatoCampoEnsayo.Numero, "mm", null, 6);
        Campo("Area", "Área", TipoDatoCampoEnsayo.Formula, "mm²", "PI()*({Diametro}/2)^2", 7);
        Campo("CargaKn", "Carga máx", TipoDatoCampoEnsayo.Numero, "kN", null, 8);
        Campo("CargaPsi", "Carga máx", TipoDatoCampoEnsayo.Numero, "psi", null, 9);
        Campo("FcDiseno", "f'c diseño", TipoDatoCampoEnsayo.Numero, "psi", null, 10);
        Campo("Cumplimiento", "Cumplimiento", TipoDatoCampoEnsayo.Formula, null, "{CargaPsi} / {FcDiseno}", 11);
        Campo("Seguimiento", "Seguimiento", TipoDatoCampoEnsayo.Formula, null,
            "IIF({Edad} == 7, IIF({Cumplimiento} < 0.7, \"Revisar\", \"Ok\"), " +
            "IIF({Edad} == 14, IIF({Cumplimiento} < 0.85, \"Revisar\", \"Ok\"), " +
            "IIF({Edad} == 28, IIF({Cumplimiento} < 0.99, \"Revisar\", \"Ok\"), " +
            "IIF({Edad} > 28, IIF({Cumplimiento} < 1, \"Revisar\", \"Ok\"), \"No aplica\"))))", 12);
        Campo("Observaciones", "Observaciones", TipoDatoCampoEnsayo.Texto, null, null, 13);
    }
}
