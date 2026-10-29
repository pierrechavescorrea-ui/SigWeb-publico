using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Tablas;

// Carga y guardado de los campos de una tabla.
public partial class MotorTabla
{
    // Carga la configuración de la tabla y registra (si faltan) sus columnas nativas.
    // filas: ids de las filas actuales con su etiqueta (para el asistente de fórmulas).
    public static async Task<MotorTabla> CargarAsync(AppDbContext db, string tabla, IEnumerable<ColumnaNativa> nativas, IEnumerable<(int Id, string Etiqueta)> filas)
    {
        var m = new MotorTabla(tabla);
        foreach (var n in nativas) m.Nativas[n.Clave] = n;

        var campos = await db.CamposTabla.Where(c => c.Tabla == tabla).ToListAsync();
        int orden = campos.Count == 0 ? 0 : campos.Max(c => c.Orden) + 1;
        bool nuevos = false;
        foreach (var n in m.Nativas.Values)
        {
            var c = campos.FirstOrDefault(x => x.Origen == OrigenCampoTabla.Nativo && string.Equals(x.Clave, n.Clave, StringComparison.OrdinalIgnoreCase));
            if (c is null)
            {
                c = new CampoTabla { Tabla = tabla, Clave = n.Clave, Nombre = n.Nombre, Origen = OrigenCampoTabla.Nativo, Tipo = n.Tipo, Orden = orden++, Visible = n.VisiblePorDefecto };
                db.CamposTabla.Add(c);
                campos.Add(c);
                nuevos = true;
            }
            else if (c.Tipo != n.Tipo && !c.TipoFijadoPorUsuario) { c.Tipo = n.Tipo; nuevos = true; }
        }
        if (nuevos) await db.SaveChangesAsync();

        // Nativas que la página ya no declara no se muestran (quedan guardadas por si vuelven).
        m.Campos = campos.Where(c => !c.Eliminado && (c.Origen != OrigenCampoTabla.Nativo || m.Nativas.ContainsKey(c.Clave))).ToList();
        m.Filas = filas.ToList();
        await m.RecargarDatosAsync(db);
        return m;
    }

    // Igual que CargarAsync pero solo lee: no registra columnas nativas ni cambia tipos (Dossier,
    // exportaciones fuera de la página). Las nativas que la página aún no registró quedan al final.
    public static async Task<MotorTabla> CargarLecturaAsync(AppDbContext db, string tabla, IEnumerable<ColumnaNativa> nativas, IEnumerable<(int Id, string Etiqueta)> filas)
    {
        var m = new MotorTabla(tabla);
        foreach (var n in nativas) m.Nativas[n.Clave] = n;
        var campos = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == tabla && !c.Eliminado).ToListAsync();
        campos = campos.Where(c => c.Origen != OrigenCampoTabla.Nativo || m.Nativas.ContainsKey(c.Clave)).ToList();
        int orden = campos.Count == 0 ? 0 : campos.Max(c => c.Orden) + 1, id = -1;
        foreach (var n in m.Nativas.Values)
            if (!campos.Any(c => c.Origen == OrigenCampoTabla.Nativo && string.Equals(c.Clave, n.Clave, StringComparison.OrdinalIgnoreCase)))
                campos.Add(new CampoTabla { Id = id--, Tabla = tabla, Clave = n.Clave, Nombre = n.Nombre, Origen = OrigenCampoTabla.Nativo, Tipo = n.Tipo, Orden = orden++, Visible = n.VisiblePorDefecto });
        m.Campos = campos;
        m.Filas = filas.ToList();
        await m.RecargarDatosAsync(db);
        return m;
    }

    public async Task RecargarDatosAsync(AppDbContext db)
    {
        InvalidarConteos();
        var ids = Campos.Select(c => c.Id).ToList();
        Valores = (await db.ValoresCampoTabla.AsNoTracking().Where(v => ids.Contains(v.CampoTablaId)).ToListAsync())
            .GroupBy(v => (v.CampoTablaId, v.FilaId)).ToDictionary(g => g.Key, g => g.First());
        Formatos = (await db.FormatosCondicionales.AsNoTracking().Where(f => ids.Contains(f.CampoTablaId)).ToListAsync())
            .GroupBy(f => f.CampoTablaId).ToDictionary(g => g.Key, g => g.OrderBy(f => f.Orden).ToList());
        var tipos = Campos.Where(c => c.Tipo == TipoCampoTabla.Lista && !string.IsNullOrWhiteSpace(c.CatalogoTipo)).Select(c => c.CatalogoTipo!).Distinct().ToList();
        OpcionesCatalogo = (await db.Catalogos.AsNoTracking().Where(i => tipos.Contains(i.Tipo) && i.Activo).ToListAsync())
            .GroupBy(i => i.Tipo).ToDictionary(g => g.Key, g => g.Select(i => i.Nombre).OrderBy(n => n).ToList());
    }

    // Tipos de catálogo disponibles para listas desplegables.
    public static async Task<List<string>> TiposCatalogoAsync(AppDbContext db) =>
        await db.Catalogos.AsNoTracking().Where(i => i.Activo).Select(i => i.Tipo).Distinct().OrderBy(t => t).ToListAsync();

    // ---------- Valores de celda ----------

    public async Task GuardarValorAsync(AppDbContext db, int fila, CampoTabla c, string? valor)
    {
        var r = await db.ValoresCampoTabla.FirstOrDefaultAsync(v => v.CampoTablaId == c.Id && v.FilaId == fila);
        if (r is null) { r = new ValorCampoTabla { CampoTablaId = c.Id, FilaId = fila }; db.ValoresCampoTabla.Add(r); }
        r.Valor = string.IsNullOrWhiteSpace(valor) ? null : valor;
        await db.SaveChangesAsync();
        InvalidarConteos();
        Valores[(c.Id, fila)] = new ValorCampoTabla { Id = r.Id, CampoTablaId = c.Id, FilaId = fila, Valor = r.Valor, Formula = r.Formula };
    }

    // Fórmula solo para una celda (null = quitarla).
    public async Task GuardarFormulaCeldaAsync(AppDbContext db, int fila, CampoTabla c, string? formula)
    {
        var r = await db.ValoresCampoTabla.FirstOrDefaultAsync(v => v.CampoTablaId == c.Id && v.FilaId == fila);
        if (r is null) { r = new ValorCampoTabla { CampoTablaId = c.Id, FilaId = fila }; db.ValoresCampoTabla.Add(r); }
        r.Formula = string.IsNullOrWhiteSpace(formula) ? null : formula.Trim();
        await db.SaveChangesAsync();
        Valores[(c.Id, fila)] = new ValorCampoTabla { Id = r.Id, CampoTablaId = c.Id, FilaId = fila, Valor = r.Valor, Formula = r.Formula };
    }

    // Columnas que trae un Excel y la tabla no tiene: se crean como campos de origen Excel y se guardan sus valores.
    // extras: filaId → (encabezado → valor de la celda).
    public static async Task GuardarExtrasExcelAsync(AppDbContext db, string tabla, Dictionary<int, Dictionary<string, string>> extras)
    {
        var encabezados = extras.Values.SelectMany(d => d.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (encabezados.Count == 0) return;
        var campos = await db.CamposTabla.Where(c => c.Tabla == tabla && !c.Eliminado).ToListAsync();
        int orden = campos.Count == 0 ? 0 : campos.Max(c => c.Orden) + 1;
        var porNombre = new Dictionary<string, CampoTabla>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in encabezados)
        {
            var c = campos.FirstOrDefault(x => string.Equals(x.Nombre.Trim(), e.Trim(), StringComparison.OrdinalIgnoreCase));
            if (c is null)
            {
                // Columna nueva cuyas celdas con dato son todas fechas (el lector las da dd/MM/yyyy): tipo Fecha.
                var datos = extras.Values.Select(d => d.TryGetValue(e, out var v) ? v : null).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                var tipo = datos.Count > 0 && datos.All(v => FechaExcel(v) is not null) ? TipoCampoTabla.Fecha : TipoCampoTabla.Texto;
                c = new CampoTabla { Tabla = tabla, Clave = ClaveNueva("x"), Nombre = e.Trim(), Origen = OrigenCampoTabla.Excel, Tipo = tipo, Orden = orden++ };
                db.CamposTabla.Add(c);
            }
            porNombre[e] = c;
        }
        await db.SaveChangesAsync();
        var ids = porNombre.Values.Select(c => c.Id).ToList();
        var filas = extras.Keys.ToList();
        var existentes = await db.ValoresCampoTabla.Where(v => ids.Contains(v.CampoTablaId) && filas.Contains(v.FilaId)).ToListAsync();
        var mapa = existentes.GroupBy(v => (v.CampoTablaId, v.FilaId)).ToDictionary(g => g.Key, g => g.First());
        foreach (var (fila, datos) in extras)
            foreach (var (enc, valor) in datos)
            {
                // Las columnas calculadas no se pisan con el Excel.
                var c = porNombre[enc];
                if (c.Calculado) continue;
                if (!mapa.TryGetValue((c.Id, fila), out var r))
                {
                    r = new ValorCampoTabla { CampoTablaId = c.Id, FilaId = fila };
                    db.ValoresCampoTabla.Add(r);
                    mapa[(c.Id, fila)] = r;
                }
                var texto = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
                // En columnas Fecha se guarda yyyy-MM-dd (como al digitar); lo que no sea fecha queda tal cual.
                if (texto is not null && c.Tipo == TipoCampoTabla.Fecha && FechaExcel(texto) is DateTime f)
                    texto = f.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                r.Valor = texto;
            }
        await db.SaveChangesAsync();
    }

    // Fecha tal como la entrega LectorExcelGenerico (día/mes/año, con o sin hora) o ya en yyyy-MM-dd.
    private static DateTime? FechaExcel(string? v) =>
        DateTime.TryParseExact(v?.Trim(), new[] { "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "yyyy-MM-dd" },
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var f) ? f : null;

    public static string ClaveNueva(string prefijo) => $"{prefijo}_{Guid.NewGuid():N}"[..14];

    // ---------- Campos ----------

    public async Task<int> GuardarCampoAsync(AppDbContext db, CampoTabla datos)
    {
        CampoTabla c;
        if (datos.Id == 0)
        {
            c = new CampoTabla
            {
                Tabla = Tabla, Clave = ClaveNueva("c"), Origen = OrigenCampoTabla.Manual,
                Orden = Campos.Count == 0 ? 0 : Campos.Max(x => x.Orden) + 1, Visible = true
            };
            db.CamposTabla.Add(c);
        }
        else
        {
            c = await db.CamposTabla.FirstAsync(x => x.Id == datos.Id);
            // Al renombrar, se actualizan las fórmulas que lo citan.
            if (!string.Equals(c.Nombre, datos.Nombre, StringComparison.Ordinal))
            {
                var viejo = c.Nombre;
                foreach (var otro in await db.CamposTabla.Where(x => x.Tabla == Tabla && x.Formula != null).ToListAsync())
                    otro.Formula = FormulaExcel.Renombrar(otro.Formula!, viejo, datos.Nombre);
                var idsTabla = Campos.Select(x => x.Id).ToList();
                foreach (var v in await db.ValoresCampoTabla.Where(x => idsTabla.Contains(x.CampoTablaId) && x.Formula != null).ToListAsync())
                    v.Formula = FormulaExcel.Renombrar(v.Formula!, viejo, datos.Nombre);
                foreach (var f in await db.FormatosCondicionales.Where(x => idsTabla.Contains(x.CampoTablaId) && x.Operador == OperadorFormato.Formula).ToListAsync())
                    if (f.Valor1 is not null) f.Valor1 = FormulaExcel.Renombrar(f.Valor1, viejo, datos.Nombre);
            }
        }
        c.Nombre = datos.Nombre.Trim();
        if (c.Origen == OrigenCampoTabla.Nativo)
        {
            // En columnas propias solo se cambia cómo se interpreta el valor (para mostrar y para los formatos).
            if (c.Tipo != datos.Tipo) { c.Tipo = datos.Tipo; c.TipoFijadoPorUsuario = true; }
        }
        else
        {
            c.Tipo = datos.Tipo;
            c.Calculado = datos.Calculado;
            c.Formula = datos.Calculado ? datos.Formula?.Trim() : null;
            c.CatalogoTipo = datos.Tipo == TipoCampoTabla.Lista && !string.IsNullOrWhiteSpace(datos.CatalogoTipo) ? datos.CatalogoTipo : null;
            c.Opciones = datos.Tipo == TipoCampoTabla.Lista && c.CatalogoTipo is null ? datos.Opciones : null;
        }
        c.Decimales = datos.Decimales;
        await db.SaveChangesAsync();
        var id = c.Id;
        await RefrescarCamposAsync(db);
        return id;
    }

    public async Task CambiarVisibleAsync(AppDbContext db, CampoTabla c, bool visible)
    {
        var e = await db.CamposTabla.FirstAsync(x => x.Id == c.Id);
        e.Visible = visible; c.Visible = visible;
        await db.SaveChangesAsync();
    }

    // Mueve el campo una posición (dir = −1 arriba, +1 abajo) y renumera.
    // Arrastrar y soltar: el campo queda en la posición del campo destino.
    public async Task MoverAAsync(AppDbContext db, CampoTabla c, CampoTabla destino)
    {
        var lista = Ordenados.ToList();
        int i = lista.FindIndex(x => x.Id == c.Id), j = lista.FindIndex(x => x.Id == destino.Id);
        if (i < 0 || j < 0 || i == j) return;
        var mov = lista[i];
        lista.RemoveAt(i);
        lista.Insert(j, mov);
        var ents = await db.CamposTabla.Where(x => x.Tabla == Tabla).ToListAsync();
        for (int k = 0; k < lista.Count; k++)
        {
            lista[k].Orden = k;
            var e = ents.FirstOrDefault(x => x.Id == lista[k].Id);
            if (e is not null) e.Orden = k;
        }
        await db.SaveChangesAsync();
    }

    // Qué se afecta al eliminar un campo (aviso previo).
    public (int Valores, int Formatos, List<string> Formulas) Impacto(CampoTabla c)
    {
        int valores = Valores.Values.Count(v => v.CampoTablaId == c.Id && (v.Valor is not null || v.Formula is not null));
        int formatos = Formatos.TryGetValue(c.Id, out var l) ? l.Count : 0;
        var formulas = Campos.Where(x => x.Id != c.Id && FormulaExcel.Referencias(x.Formula).Any(n => string.Equals(n, c.Nombre, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Nombre).ToList();
        return (valores, formatos, formulas);
    }

    // Eliminar = Papelera (solo Manual/Excel; los nativos se ocultan).
    public async Task EliminarAsync(AppDbContext db, CampoTabla c)
    {
        if (c.Origen == OrigenCampoTabla.Nativo) return;
        var e = await db.CamposTabla.FirstAsync(x => x.Id == c.Id);
        e.Eliminado = true; e.FechaEliminacion = DateTime.Now;
        await db.SaveChangesAsync();
        Campos.RemoveAll(x => x.Id == c.Id);
    }

    private async Task RefrescarCamposAsync(AppDbContext db)
    {
        db.ChangeTracker.Clear();
        var campos = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == Tabla && !c.Eliminado).ToListAsync();
        Campos = campos.Where(c => c.Origen != OrigenCampoTabla.Nativo || Nativas.ContainsKey(c.Clave)).ToList();
        await RecargarDatosAsync(db);
    }

    // ---------- Formatos condicionales ----------

    public async Task GuardarFormatoAsync(AppDbContext db, FormatoCondicional datos)
    {
        FormatoCondicional f;
        if (datos.Id == 0)
        {
            f = new FormatoCondicional { CampoTablaId = datos.CampoTablaId };
            f.Orden = Formatos.TryGetValue(datos.CampoTablaId, out var l) && l.Count > 0 ? l.Max(x => x.Orden) + 1 : 0;
            db.FormatosCondicionales.Add(f);
        }
        else f = await db.FormatosCondicionales.FirstAsync(x => x.Id == datos.Id);
        f.Operador = datos.Operador; f.Valor1 = datos.Valor1; f.Valor2 = datos.Valor2;
        f.Estilo = datos.Estilo; f.ColorTexto = datos.ColorTexto; f.ColorFondo = datos.ColorFondo;
        f.Negrita = datos.Negrita; f.Forma = datos.Forma; f.Activo = datos.Activo;
        await db.SaveChangesAsync();
        await RecargarDatosAsync(db);
    }

    public async Task EliminarFormatoAsync(AppDbContext db, FormatoCondicional f)
    {
        var e = await db.FormatosCondicionales.FirstOrDefaultAsync(x => x.Id == f.Id);
        if (e is not null) { db.FormatosCondicionales.Remove(e); await db.SaveChangesAsync(); }
        await RecargarDatosAsync(db);
    }

    public async Task MoverFormatoAAsync(AppDbContext db, FormatoCondicional f, FormatoCondicional destino)
    {
        if (!Formatos.TryGetValue(f.CampoTablaId, out var l)) return;
        var lista = l.OrderBy(x => x.Orden).ToList();
        int i = lista.FindIndex(x => x.Id == f.Id), j = lista.FindIndex(x => x.Id == destino.Id);
        if (i < 0 || j < 0 || i == j) return;
        var mov = lista[i];
        lista.RemoveAt(i);
        lista.Insert(j, mov);
        var ids = lista.Select(x => x.Id).ToList();
        var ents = await db.FormatosCondicionales.Where(x => ids.Contains(x.Id)).ToListAsync();
        for (int k = 0; k < lista.Count; k++) { var e = ents.First(x => x.Id == lista[k].Id); e.Orden = k; }
        await db.SaveChangesAsync();
        await RecargarDatosAsync(db);
    }
}
