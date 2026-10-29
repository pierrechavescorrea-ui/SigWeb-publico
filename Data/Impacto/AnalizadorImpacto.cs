using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Impacto;

public enum NivelImpacto { Bajo = 0, Medio = 1, Alto = 2, Critico = 3 }

// Una cosa de la app que depende del elemento que se va a eliminar.
public class DependenciaImpacto
{
    public string Menu { get; set; } = "";       // dónde se vería afectado (menú/visualización)
    public string Detalle { get; set; } = "";    // qué pasaría
    public int Cantidad { get; set; }
    public int Peso { get; set; }                // qué tan vital es cada unidad (1 = accesorio … 10 = estructural)
    public int Puntos => Cantidad * Peso;
}

public class InformeImpacto
{
    public string Elemento { get; set; } = "";
    public NivelImpacto Nivel { get; set; } = NivelImpacto.Bajo;
    public int Puntaje { get; set; }
    public List<DependenciaImpacto> Dependencias { get; set; } = new();
    public string Recomendacion { get; set; } = "";
    public bool RequiereEntender => Nivel >= NivelImpacto.Alto;
}

// Algoritmo interno de impacto: cada elemento eliminable consulta de qué
// depende el resto de la app (hoja de vida, Matriz PIE, ensayos, planos,
// materiales, adjuntos, Línea 3D…), suma "puntos" (cantidad × peso de cada
// dependencia) y clasifica la gravedad. Además de los umbrales por puntaje,
// hay reglas que fuerzan un mínimo (ej. una torre con datos de PIE no baja
// de "Alto"). Todo lo eliminado va a la Papelera, así que el aviso es previo.
public class AnalizadorImpacto
{
    private readonly AppDbContext _db;
    public AnalizadorImpacto(AppDbContext db) { _db = db; }

    // ---------- Pesos ----------
    private const int PesoRegistroPie = 4, PesoAdjunto = 1, PesoEnsayo = 3, PesoPlano = 2, PesoMaterial = 3,
                      PesoArchivoComp = 1, PesoComponente = 2, PesoCampoExtra = 1, PesoVisor = 6, PesoCatalogo = 3, PesoInforme = 2;

    private static void Cerrar(InformeImpacto r, NivelImpacto minimo = NivelImpacto.Bajo)
    {
        r.Dependencias = r.Dependencias.Where(d => d.Cantidad > 0).OrderByDescending(d => d.Puntos).ToList();
        r.Puntaje = r.Dependencias.Sum(d => d.Puntos);
        var nivel = r.Puntaje switch { < 6 => NivelImpacto.Bajo, < 25 => NivelImpacto.Medio, < 80 => NivelImpacto.Alto, _ => NivelImpacto.Critico };
        r.Nivel = (NivelImpacto)Math.Max((int)nivel, (int)minimo);
        r.Recomendacion = r.Nivel switch
        {
            NivelImpacto.Critico => "Es vital para el funcionamiento: revisa cada menú afectado antes de continuar. Puedes recuperarlo desde la Papelera, pero mientras tanto esas vistas quedarán incompletas.",
            NivelImpacto.Alto => "Afecta varios menús. Confirma que es lo que quieres; se podrá restaurar desde la Papelera.",
            NivelImpacto.Medio => "Afecta algunos datos relacionados. Se podrá restaurar desde la Papelera.",
            _ => r.Dependencias.Count == 0 ? "No hay otros datos que dependan de esto." : "Impacto menor. Se podrá restaurar desde la Papelera."
        };
    }

    private static void Agregar(InformeImpacto r, string menu, string detalle, int cantidad, int peso)
    {
        if (cantidad > 0) r.Dependencias.Add(new DependenciaImpacto { Menu = menu, Detalle = detalle, Cantidad = cantidad, Peso = peso });
    }

    // ---------- Torres (una o toda una capa) ----------
    public Task<InformeImpacto> Torre(int torreId) => Torres(new List<int> { torreId });

    public async Task<InformeImpacto> Capa(int capaId)
    {
        var ids = await _db.ElementosTrazado.AsNoTracking().Where(e => e.CapaTrazadoId == capaId && !e.Eliminado).Select(e => e.Id).ToListAsync();
        return await Torres(ids);
    }

    public async Task<InformeImpacto> Torres(List<int> ids)
    {
        var r = new InformeImpacto { Elemento = ids.Count == 1 ? "Torre" : $"{ids.Count} torres" };
        if (ids.Count == 0) { Cerrar(r); return r; }

        var torres = await _db.ElementosTrazado.AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync();
        if (ids.Count == 1 && torres.Count == 1) r.Elemento = $"Torre {torres[0].Codigo}";

        var comps = await _db.Componentes.AsNoTracking().Where(c => ids.Contains(c.ElementoTrazadoId) && !c.Eliminado).Select(c => new { c.Id, c.EsHojaDeVida }).ToListAsync();
        var idsComp = comps.Select(c => c.Id).ToList();

        var registros = await _db.RegistrosParametro.AsNoTracking().Where(x => idsComp.Contains(x.ComponenteId)).Select(x => new { x.Id, x.Completado }).ToListAsync();
        var idsReg = registros.Select(x => x.Id).ToList();
        int pie = registros.Count(x => x.Completado);
        int adjPie = await _db.ArchivosRegistroParametro.AsNoTracking().CountAsync(a => idsReg.Contains(a.RegistroParametroId) && !a.Eliminado);
        int archComp = await _db.ArchivosComponente.AsNoTracking().CountAsync(a => idsComp.Contains(a.ComponenteId) && !a.Eliminado);

        var idsEnsDirectos = await _db.ResultadosEnsayo.AsNoTracking().Where(x => x.ElementoTrazadoId != null && ids.Contains(x.ElementoTrazadoId.Value) && !x.Eliminado).Select(x => x.Id).ToListAsync();
        var idsEnsVia = await _db.ResultadosEnsayoComponente.AsNoTracking().Where(x => idsComp.Contains(x.ComponenteId)).Select(x => x.ResultadoEnsayoId).ToListAsync();
        int ensayos = idsEnsDirectos.Union(idsEnsVia).Count();

        int materiales = await _db.UbicacionesMaterial.AsNoTracking().Where(u => (u.ElementoTrazadoId != null && ids.Contains(u.ElementoTrazadoId.Value)) || (u.ComponenteId != null && idsComp.Contains(u.ComponenteId.Value))).Select(u => u.MaterialId).Distinct().CountAsync();
        int planos = await _db.ComponentesPlano.AsNoTracking().CountAsync(p => idsComp.Contains(p.ComponenteId));
        int extrasTorre = await _db.CamposExtraTorre.AsNoTracking().CountAsync(c => ids.Contains(c.ElementoTrazadoId) && !c.Eliminado);
        int extrasPata = await _db.CamposExtraComponente.AsNoTracking().CountAsync(c => idsComp.Contains(c.ComponenteId) && !c.Eliminado);
        int enCatalogo = await _db.Catalogos.AsNoTracking().CountAsync(c => c.ElementoTrazadoId != null && ids.Contains(c.ElementoTrazadoId.Value) && c.Activo);
        int conCoord = torres.Count(t => t.Latitud != 0 || t.Longitud != 0);

        Agregar(r, "Matriz PIE", "estados de parámetros completados que dejarían de verse (la fila de la torre y su % de cumplimiento)", pie, PesoRegistroPie);
        Agregar(r, "Matriz PIE / Hoja de vida", "adjuntos de parámetros asociados", adjPie, PesoAdjunto);
        Agregar(r, "Ensayos", "resultados de ensayo ligados a la torre (gráficos, informes, tablas de datos)", ensayos, PesoEnsayo);
        Agregar(r, "Materiales", "materiales asociados a sus componentes", materiales, PesoMaterial);
        Agregar(r, "Planos maestros", "vínculos de planos a sus componentes", planos, PesoPlano);
        Agregar(r, "Trazado (ficha de la torre)", "patas, elementos y hoja de vida", comps.Count, PesoComponente);
        Agregar(r, "Trazado (ficha de la torre)", "archivos adjuntos de componentes", archComp, PesoArchivoComp);
        Agregar(r, "Tabla de torres", "datos adicionales cargados (torre y patas)", extrasTorre + extrasPata, PesoCampoExtra);
        Agregar(r, "Matriz PIE / Catálogos (Localización)", "la torre sale como localización seleccionable", enCatalogo, PesoCatalogo);
        Agregar(r, "Línea 3D / Modelos 3D / mapa", "la torre desaparece del visor 3D y del mapa", conCoord, PesoVisor);

        var min = NivelImpacto.Bajo;
        if (pie > 0 || ensayos > 0 || materiales > 0) min = NivelImpacto.Alto;
        if (torres.Count > 1 && (pie > 0 || ensayos > 0)) min = NivelImpacto.Critico;
        Cerrar(r, min);
        return r;
    }

    // ---------- Componente (pata, elemento adicional) ----------
    public async Task<InformeImpacto> Componente(int componenteId)
    {
        var c = await _db.Componentes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == componenteId);
        var r = new InformeImpacto { Elemento = c is null ? "Elemento" : $"Elemento \"{c.Nombre}\"" };
        if (c is null) { Cerrar(r); return r; }
        var t = await _db.ElementosTrazado.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.ElementoTrazadoId);
        if (t is not null) r.Elemento += $" de la torre {t.Codigo}";

        var registros = await _db.RegistrosParametro.AsNoTracking().Where(x => x.ComponenteId == componenteId).Select(x => new { x.Id, x.Completado }).ToListAsync();
        var idsReg = registros.Select(x => x.Id).ToList();
        int pie = registros.Count(x => x.Completado);
        int adj = await _db.ArchivosRegistroParametro.AsNoTracking().CountAsync(a => idsReg.Contains(a.RegistroParametroId) && !a.Eliminado);
        int arch = await _db.ArchivosComponente.AsNoTracking().CountAsync(a => a.ComponenteId == componenteId && !a.Eliminado);
        int ens = await _db.ResultadosEnsayoComponente.AsNoTracking().CountAsync(x => x.ComponenteId == componenteId);
        int mat = await _db.UbicacionesMaterial.AsNoTracking().Where(u => u.ComponenteId == componenteId).Select(u => u.MaterialId).Distinct().CountAsync();
        int pla = await _db.ComponentesPlano.AsNoTracking().CountAsync(p => p.ComponenteId == componenteId);
        int ext = await _db.CamposExtraComponente.AsNoTracking().CountAsync(x => x.ComponenteId == componenteId && !x.Eliminado);

        Agregar(r, "Matriz PIE", "estados de parámetros completados de este elemento", pie, PesoRegistroPie);
        Agregar(r, "Matriz PIE / Hoja de vida", "adjuntos de parámetros", adj, PesoAdjunto);
        Agregar(r, "Trazado (ficha)", "archivos adjuntos del elemento", arch, PesoArchivoComp);
        Agregar(r, "Ensayos", "resultados de ensayo vinculados al elemento", ens, PesoEnsayo);
        Agregar(r, "Materiales", "materiales asociados", mat, PesoMaterial);
        Agregar(r, "Planos maestros", "planos vinculados", pla, PesoPlano);
        Agregar(r, "Tabla de torres", "datos adicionales de la pata", ext, PesoCampoExtra);
        Cerrar(r, c.EsHojaDeVida ? NivelImpacto.Critico : (ens > 0 || mat > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo));
        return r;
    }

    // ---------- Adjuntos ----------
    public async Task<InformeImpacto> AdjuntoRegistro(int archivoId)
    {
        var a = await _db.ArchivosRegistroParametro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == archivoId);
        var r = new InformeImpacto { Elemento = a is null ? "Adjunto" : $"Adjunto \"{a.NombreOriginal}\"" };
        if (a is null) { Cerrar(r); return r; }
        var reg = await _db.RegistrosParametro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.RegistroParametroId);
        if (reg is not null)
        {
            var p = await _db.ParametrosActividad.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reg.ParametroActividadId);
            var comp = await _db.Componentes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reg.ComponenteId);
            var t = comp is null ? null : await _db.ElementosTrazado.AsNoTracking().FirstOrDefaultAsync(x => x.Id == comp.ElementoTrazadoId);
            int otros = await _db.ArchivosRegistroParametro.AsNoTracking().CountAsync(x => x.RegistroParametroId == reg.Id && !x.Eliminado && x.Id != archivoId);
            var donde = $"{(p?.Nombre ?? "parámetro")} de la torre {t?.Codigo ?? "?"}";
            Agregar(r, "Matriz PIE", $"el ícono de documento de «{donde}» " + (otros == 0 ? "desaparece" : "queda con menos archivos"), 1, 1);
            Agregar(r, "Hoja de vida de la torre", "el soporte documental del parámetro", 1, 1);
            if (otros == 0 && reg.Completado && reg.EstadoParametroId != null)
                Agregar(r, "Matriz PIE", "el estado queda sin ningún soporte documental (auditoría)", 1, 4);
        }
        Cerrar(r);
        return r;
    }

    public async Task<InformeImpacto> AdjuntoComponente(int archivoId)
    {
        var a = await _db.ArchivosComponente.AsNoTracking().FirstOrDefaultAsync(x => x.Id == archivoId);
        var r = new InformeImpacto { Elemento = a is null ? "Adjunto" : $"Adjunto \"{a.NombreOriginal}\"" };
        if (a is not null)
        {
            var comp = await _db.Componentes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.ComponenteId);
            Agregar(r, "Trazado (ficha del elemento)", $"el archivo deja de listarse en \"{comp?.Nombre ?? "el elemento"}\"", 1, 1);
        }
        Cerrar(r);
        return r;
    }

    // ---------- Ensayos ----------
    public async Task<InformeImpacto> ResultadoEnsayo(int resultadoId)
    {
        var x = await _db.ResultadosEnsayo.AsNoTracking().FirstOrDefaultAsync(e => e.Id == resultadoId);
        var r = new InformeImpacto { Elemento = "Resultado de ensayo" };
        if (x is null) { Cerrar(r); return r; }
        var tipo = await _db.TiposEnsayo.AsNoTracking().FirstOrDefaultAsync(t => t.Id == x.TipoEnsayoId);
        r.Elemento = $"Resultado de {tipo?.Nombre ?? "ensayo"}" + (string.IsNullOrWhiteSpace(x.ElementoTexto) ? "" : $" ({x.ElementoTexto})");
        Agregar(r, "Ensayos (tabla y gráficos)", "el dato sale de las tablas y de los gráficos del tipo de ensayo", 1, 2);
        if (x.InformeId != null)
        {
            int hermanos = await _db.ResultadosEnsayo.AsNoTracking().CountAsync(e => e.InformeId == x.InformeId && !e.Eliminado && e.Id != x.Id);
            Agregar(r, "Ensayos (informes)", "el informe al que pertenece queda con un resultado menos", 1, PesoInforme);
            if (hermanos == 0) Agregar(r, "Ensayos (informes)", "es el único resultado del informe: el informe queda vacío", 1, 3);
        }
        if (x.ElementoTrazadoId != null)
        {
            var t = await _db.ElementosTrazado.AsNoTracking().FirstOrDefaultAsync(e => e.Id == x.ElementoTrazadoId);
            Agregar(r, "Trazado (ficha de la torre)", $"el ensayo deja de aparecer en la torre {t?.Codigo ?? "?"}", 1, 2);
        }
        int vinc = await _db.ResultadosEnsayoComponente.AsNoTracking().CountAsync(e => e.ResultadoEnsayoId == x.Id);
        Agregar(r, "Hoja de vida / patas", "vínculos con patas o elementos", vinc, 2);
        Cerrar(r);
        return r;
    }

    public async Task<InformeImpacto> LoteEnsayos(int tipoEnsayoId, string origen)
    {
        var ids = await _db.ResultadosEnsayo.AsNoTracking().Where(x => x.TipoEnsayoId == tipoEnsayoId && x.Origen == origen && !x.Eliminado).Select(x => x.Id).ToListAsync();
        var tipo = await _db.TiposEnsayo.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tipoEnsayoId);
        var r = new InformeImpacto { Elemento = $"{ids.Count} resultado(s) de {tipo?.Nombre ?? "ensayo"} — carga \"{origen}\"" };
        int conTorre = await _db.ResultadosEnsayo.AsNoTracking().CountAsync(x => ids.Contains(x.Id) && x.ElementoTrazadoId != null);
        int informes = await _db.ResultadosEnsayo.AsNoTracking().Where(x => ids.Contains(x.Id) && x.InformeId != null).Select(x => x.InformeId).Distinct().CountAsync();
        Agregar(r, "Ensayos (tabla y gráficos)", "resultados que salen de tablas y gráficos", ids.Count, 2);
        Agregar(r, "Trazado (ficha de la torre)", "resultados ligados a torres", conTorre, 2);
        Agregar(r, "Ensayos (informes)", "informes que quedarían con menos resultados", informes, 3);
        Cerrar(r);
        return r;
    }

    // ---------- Carga completa de la Tabla de torres ----------
    public async Task<InformeImpacto> CargaTablaTorres(string origen)
    {
        var campos = await _db.CamposExtraTorre.AsNoTracking().Where(c => c.Origen == origen && !c.Eliminado).Select(c => new { c.ElementoTrazadoId, c.Clave }).ToListAsync();
        var camposPata = await _db.CamposExtraComponente.AsNoTracking().CountAsync(c => c.Origen == origen && !c.Eliminado);
        var r = new InformeImpacto { Elemento = $"Carga \"{origen}\"" };
        var torres = campos.Select(c => c.ElementoTrazadoId).Distinct().ToList();
        var claves = campos.Select(c => c.Clave).Distinct().ToList();

        // columnas que SOLO existen por esta carga: desaparecerían de la tabla
        var otras = await _db.CamposExtraTorre.AsNoTracking().Where(c => c.Origen != origen && !c.Eliminado).Select(c => c.Clave).Distinct().ToListAsync();
        var unicas = claves.Where(k => !otras.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();

        Agregar(r, "Tabla de torres", $"datos en {claves.Count} columna(s) y {torres.Count} torre(s)", campos.Count, PesoCampoExtra);
        Agregar(r, "Tabla de torres", $"columna(s) que quedarían sin ningún dato: {string.Join(", ", unicas.Take(8))}{(unicas.Count > 8 ? "…" : "")}", unicas.Count, 3);
        Agregar(r, "Tabla de torres", "datos de patas de esta carga", camposPata, PesoCampoExtra);
        Agregar(r, "Trazado (ficha de la torre)", "torres que pierden su información adicional", torres.Count, 2);

        // ¿incluye el campo que define el tipo de torre (Línea 3D / Modelos 3D)?
        var tipo = claves.FirstOrDefault(c => c.Contains("cuerpo", StringComparison.OrdinalIgnoreCase))
            ?? claves.FirstOrDefault(c => c.Contains("tipo", StringComparison.OrdinalIgnoreCase)
                && !c.Contains("cimenta", StringComparison.OrdinalIgnoreCase) && !c.Contains("suelo", StringComparison.OrdinalIgnoreCase));
        if (tipo is not null)
            Agregar(r, "Línea 3D / Modelos 3D", $"el campo de tipo de torre «{tipo}»: las torres dejan de asociarse a su modelo y se dibujan sin modelo real", torres.Count, 4);

        // torres afectadas que además tienen avance PIE (la Línea 3D lo colorea)
        var comps = await _db.Componentes.AsNoTracking().Where(c => torres.Contains(c.ElementoTrazadoId) && c.EsHojaDeVida && !c.Eliminado).Select(c => c.Id).ToListAsync();
        int conPie = await _db.RegistrosParametro.AsNoTracking().Where(x => comps.Contains(x.ComponenteId) && x.Completado).Select(x => x.ComponenteId).Distinct().CountAsync();
        Agregar(r, "Línea 3D (color por avance)", "torres con avance PIE cuya clasificación depende de estos datos", tipo is null ? 0 : conPie, 2);

        var min = campos.Count > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo;
        if (tipo is not null && torres.Count > 10) min = NivelImpacto.Critico;
        Cerrar(r, min);
        return r;
    }

    // ---------- Ensayos: tipos y grupos ----------
    public async Task<InformeImpacto> TipoEnsayo(int tipoId)
    {
        var t = await _db.TiposEnsayo.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == tipoId);
        var r = new InformeImpacto { Elemento = t is null ? "Tipo de ensayo" : $"Tipo de ensayo \"{t.Nombre}\"" };
        if (t is null) { Cerrar(r); return r; }
        await AgregarDependenciasTipo(r, new List<int> { tipoId });
        Cerrar(r, r.Dependencias.Any(d => d.Menu.StartsWith("Ensayos")) ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    public async Task<InformeImpacto> GrupoEnsayo(int grupoId)
    {
        var g = await _db.GruposEnsayo.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == grupoId);
        var r = new InformeImpacto { Elemento = g is null ? "Grupo de ensayos" : $"Grupo de ensayos \"{g.Nombre}\"" };
        if (g is null) { Cerrar(r); return r; }
        var tipos = await _db.TiposEnsayo.AsNoTracking().Where(t => t.GrupoEnsayoId == grupoId).Select(t => t.Id).ToListAsync();
        Agregar(r, "Ensayos", "tipos de ensayo del grupo que también se eliminan", tipos.Count, 3);
        await AgregarDependenciasTipo(r, tipos);
        Cerrar(r, tipos.Count > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    private async Task AgregarDependenciasTipo(InformeImpacto r, List<int> tipos)
    {
        if (tipos.Count == 0) return;
        var resultados = await _db.ResultadosEnsayo.AsNoTracking().Where(x => tipos.Contains(x.TipoEnsayoId) && !x.Eliminado).Select(x => new { x.Id, x.ElementoTrazadoId, x.InformeId }).ToListAsync();
        int torres = resultados.Where(x => x.ElementoTrazadoId != null).Select(x => x.ElementoTrazadoId).Distinct().Count();
        int informes = resultados.Where(x => x.InformeId != null).Select(x => x.InformeId).Distinct().Count();
        int campos = await _db.CamposEnsayo.AsNoTracking().CountAsync(c => tipos.Contains(c.TipoEnsayoId));
        var actividades = await _db.Actividades.AsNoTracking().Where(a => a.TipoEnsayoId != null && tipos.Contains(a.TipoEnsayoId.Value)).Select(a => a.Id).ToListAsync();
        var parametros = await _db.ParametrosActividad.AsNoTracking().Where(p => actividades.Contains(p.ActividadId)).Select(p => p.Id).ToListAsync();
        int pie = await _db.RegistrosParametro.AsNoTracking().CountAsync(x => parametros.Contains(x.ParametroActividadId) && x.Completado);

        Agregar(r, "Ensayos (tabla, Vista Excel y gráficos)", "resultados de ensayo que dejarían de verse", resultados.Count, PesoEnsayo);
        Agregar(r, "Ensayos (informes)", "informes con resultados de este tipo", informes, PesoInforme);
        Agregar(r, "Trazado (ficha de la torre)", "torres que dejan de mostrar estos ensayos", torres, 2);
        Agregar(r, "Ensayos (campos)", "campos personalizados configurados", campos, 1);
        Agregar(r, "Hoja de vida / Matriz PIE", "actividad enlazada a este ensayo se desactiva, con estados ya registrados", pie, PesoRegistroPie);
        Agregar(r, "Hoja de vida", "actividad de ensayo que se oculta de las fichas de pata", actividades.Count, 2);
    }

    // ---------- Catálogos ----------
    public async Task<InformeImpacto> Catalogo(int catalogoId)
    {
        var d = await _db.DefinicionesCatalogo.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == catalogoId);
        var r = new InformeImpacto { Elemento = d is null ? "Catálogo" : $"Catálogo \"{d.Nombre}\"" };
        if (d is null) { Cerrar(r); return r; }
        var items = await _db.Catalogos.AsNoTracking().Where(c => c.Tipo == d.Nombre).ToListAsync();
        var nombres = items.Select(i => i.Nombre).ToList();
        Agregar(r, "Catálogos", "ítems que quedan ocultos junto con el catálogo", items.Count, 1);
        var n = d.Nombre.ToLowerInvariant();
        if (n.StartsWith("localiza"))
        {
            int torres = items.Count(i => i.ElementoTrazadoId != null && i.Activo);
            Agregar(r, "Matriz PIE", "filas (una por torre) que desaparecen de la matriz", torres, 4);
            Agregar(r, "Ensayos / Materiales", "selectores de localización quedan sin opciones", items.Count > 0 ? 1 : 0, 4);
        }
        else if (n.StartsWith("material"))
            Agregar(r, "Materiales", "materiales registrados con un nombre de este catálogo", await _db.Materiales.AsNoTracking().CountAsync(m => !m.Eliminado && nombres.Contains(m.NombreMaterial)), PesoMaterial);
        else if (n.StartsWith("proveedor"))
            Agregar(r, "Materiales", "materiales registrados con un proveedor de este catálogo", await _db.Materiales.AsNoTracking().CountAsync(m => !m.Eliminado && nombres.Contains(m.Proveedor)), PesoMaterial);
        else if (n.StartsWith("criterio"))
            Agregar(r, "Materiales", "materiales con un criterio de aceptación de este catálogo", await _db.Materiales.AsNoTracking().CountAsync(m => !m.Eliminado && nombres.Contains(m.CriterioAceptacion)), PesoMaterial);
        else if (n.StartsWith("documento"))
            Agregar(r, "Materiales", "materiales con un documento de referencia de este catálogo", await _db.Materiales.AsNoTracking().CountAsync(m => !m.Eliminado && nombres.Contains(m.DocumentoReferencia)), PesoMaterial);
        Agregar(r, "Todos los formularios que lo usan", "los selectores de este catálogo quedan vacíos", items.Count > 0 ? 1 : 0, 3);
        Cerrar(r, d.EsPredeterminado ? NivelImpacto.Critico : NivelImpacto.Medio);
        return r;
    }


    // ---------- Actividades ----------
    public async Task<InformeImpacto> Documento(int id)
    {
        var d = await _db.DocumentosMaestro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        var r = new InformeImpacto { Elemento = d is null ? "Documento" : $"Documento \"{(string.IsNullOrWhiteSpace(d.Codigo) ? "#" + d.Id : d.Codigo)}\"" };
        if (d is null) { Cerrar(r); return r; }
        int versiones = await _db.VersionesDocumento.AsNoTracking().CountAsync(v => v.DocumentoMaestroId == id);
        Agregar(r, "Listado maestro de documentos", "versiones con archivo adjunto que dejarían de verse", versiones, PesoAdjunto);
        Cerrar(r);
        return r;
    }

    // ---------- Un archivo de un documento (menú del código / versiones anteriores) ----------
    public async Task<InformeImpacto> ArchivoDocumento(int versionId)
    {
        var v = await _db.VersionesDocumento.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId);
        var r = new InformeImpacto { Elemento = v is null ? "Archivo" : $"Archivo \"{v.NombreOriginal}\" (v{v.Version})" };
        if (v is null) { Cerrar(r); return r; }
        var todos = await _db.VersionesDocumento.AsNoTracking().Where(x => x.DocumentoMaestroId == v.DocumentoMaestroId)
            .OrderByDescending(x => x.FechaCarga).ThenByDescending(x => x.Id).ToListAsync();
        bool esActual = todos.FirstOrDefault(x => x.Clase == ClaseArchivoDocumento.Vigente)?.Id == v.Id;
        bool esEditable = todos.FirstOrDefault(x => x.Clase == ClaseArchivoDocumento.Editable)?.Id == v.Id;
        int cambios = await _db.ControlesCambioVersion.AsNoTracking().CountAsync(x => x.VersionDocumentoId == v.Id);
        Agregar(r, "Listado maestro de documentos", "es la versión actual: el documento pasa a mostrar la anterior", esActual ? 1 : 0, 6);
        Agregar(r, "Listado maestro de documentos", "es el editable actual", esEditable ? 1 : 0, 3);
        Agregar(r, "Listado maestro de documentos", "el documento queda sin archivos (su código deja de ser vínculo)", todos.Count == 1 ? 1 : 0, 3);
        Agregar(r, "Listado maestro de documentos", "filas de control de cambios de esta versión", cambios, 1);
        Cerrar(r, esActual ? NivelImpacto.Medio : NivelImpacto.Bajo);
        return r;
    }

    // ---------- Adjunto de un material ----------
    public async Task<InformeImpacto> AdjuntoDeMaterial(int id)
    {
        var a = await _db.AdjuntosMaterial.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        var r = new InformeImpacto { Elemento = a is null ? "Adjunto" : $"Adjunto \"{a.NombreOriginal}\" ({a.Clase})" };
        if (a is null) { Cerrar(r); return r; }
        int otros = await _db.AdjuntosMaterial.AsNoTracking().CountAsync(x => x.MaterialId == a.MaterialId && x.Id != a.Id);
        Agregar(r, "Materiales", "el material queda sin adjuntos", otros == 0 ? 1 : 0, 2);
        Cerrar(r, NivelImpacto.Bajo);
        return r;
    }

    // ---------- Cargas (Excel) de Materiales ----------
    public async Task<InformeImpacto> CargasMateriales(IReadOnlyCollection<int> ids)
    {
        var cargas = await _db.CargasMateriales.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto { Elemento = cargas.Count == 1 ? $"Carga \"{cargas[0].NombreArchivo}\"" : $"{cargas.Count} cargas de materiales" };
        var mats = await _db.Materiales.AsNoTracking().Where(m => m.CargaMaterialesId != null && ids.Contains(m.CargaMaterialesId.Value) && !m.Eliminado).ToListAsync();
        var idsMat = mats.Select(m => m.Id).ToList();
        var adj = await _db.AdjuntosMaterial.AsNoTracking().Where(a => idsMat.Contains(a.MaterialId)).Select(a => a.Clase).ToListAsync();
        int fotos = adj.Count(c => c == AdjuntoMaterial.Fotografico);
        int pdfs = adj.Count - fotos;
        int ubicados = await _db.UbicacionesMaterial.AsNoTracking().Where(u => idsMat.Contains(u.MaterialId)).Select(u => u.MaterialId).Distinct().CountAsync();
        int total = await _db.Materiales.AsNoTracking().CountAsync(m => !m.Eliminado);
        Agregar(r, "Materiales", "materiales de esta(s) carga(s) que salen del listado", mats.Count, 1);
        Agregar(r, "Materiales", "adjuntos (remisiones, certificados, otros) de esos materiales", pdfs, PesoAdjunto);
        Agregar(r, "Materiales", "registros fotográficos de esos materiales", fotos, PesoAdjunto);
        Agregar(r, "Trazado (ficha de pata)", "materiales ubicados en una pata o elemento", ubicados, PesoMaterial);
        Cerrar(r, mats.Count == 0 ? NivelImpacto.Bajo : mats.Count >= total ? NivelImpacto.Critico : ubicados > 0 || pdfs > 0 ? NivelImpacto.Alto : NivelImpacto.Medio);
        return r;
    }

    // ---------- Cargas (Excel) de Equipos ----------
    public async Task<InformeImpacto> CargasEquipos(IReadOnlyCollection<int> ids)
    {
        var cargas = await _db.CargasEquipos.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto { Elemento = cargas.Count == 1 ? $"Carga \"{cargas[0].NombreArchivo}\"" : $"{cargas.Count} cargas de equipos" };
        var calibs = await _db.IntervencionesEquipo.AsNoTracking().Where(i => i.CargaEquiposId != null && ids.Contains(i.CargaEquiposId.Value)).ToListAsync();
        var idsCal = calibs.Select(i => i.Id).ToHashSet();
        var equipos = await _db.Equipos.AsNoTracking().Where(e => e.CargaEquiposId != null && ids.Contains(e.CargaEquiposId.Value) && !e.Eliminado).Select(e => e.Id).ToListAsync();
        // Un equipo de la carga se va solo si no tiene fotos ni intervenciones de fuera de la carga.
        var otras = await _db.IntervencionesEquipo.AsNoTracking().Where(i => equipos.Contains(i.EquipoId)).ToListAsync();
        var conFotos = (await _db.FotosEquipo.AsNoTracking().Where(f => equipos.Contains(f.EquipoId)).Select(f => f.EquipoId).ToListAsync()).ToHashSet();
        int seVan = equipos.Count(e => !conFotos.Contains(e) && otras.Where(i => i.EquipoId == e).All(i => idsCal.Contains(i.Id)));
        int quedan = equipos.Count - seVan;
        int conCert = calibs.Count(i => i.RutaCertificado != null);
        Agregar(r, "Equipos", "calibraciones de esta(s) carga(s) que dejan de verse (el estado del equipo puede cambiar)", calibs.Count, 2);
        Agregar(r, "Equipos", "certificados adjuntos a esas calibraciones", conCert, PesoAdjunto);
        Agregar(r, "Equipos", "equipos que solo existen por esta(s) carga(s) y salen del listado", seVan, 2);
        Agregar(r, "Equipos", "equipos de esta(s) carga(s) que se conservan (tienen fotos u otras intervenciones)", quedan, 0);
        Cerrar(r, calibs.Count > 0 || seVan > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    // ---------- Cargas (Excel) del listado maestro de planos ----------
    public async Task<InformeImpacto> CargasPlanos(IReadOnlyCollection<int> ids)
    {
        var cargas = await _db.CargasPlanos.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto { Elemento = cargas.Count == 1 ? $"Carga \"{cargas[0].NombreArchivo}\"" : $"{cargas.Count} cargas del listado de planos" };
        var planos = await _db.PlanosMaestro.AsNoTracking().Where(p => p.CargaPlanosId != null && ids.Contains(p.CargaPlanosId.Value) && !p.Eliminado).Select(p => p.Id).ToListAsync();
        var archivos = await _db.ArchivosPlanoMaestro.AsNoTracking().Where(a => planos.Contains(a.PlanoMaestroId) && !a.Eliminado).Select(a => a.Id).ToListAsync();
        int vinc = await _db.ComponentesPlano.AsNoTracking().CountAsync(c => archivos.Contains(c.ArchivoPlanoMaestroId));
        int total = await _db.PlanosMaestro.AsNoTracking().CountAsync(p => !p.Eliminado);
        Agregar(r, "Listado maestro de planos", "planos creados por esta(s) carga(s) que salen del listado", planos.Count, 1);
        Agregar(r, "Listado maestro de planos", "archivos (PDF) de esos planos que dejan de verse", archivos.Count, 1);
        Agregar(r, "Hoja de vida / Trazado", "componentes con un plano de esta carga vinculado (pierden el enlace)", vinc, PesoPlano);
        Agregar(r, "Modelos 3D / Línea 3D", "piezas del modelo que enlazan a estos planos dejarán de abrirlos", planos.Count > 0 ? 1 : 0, 2);
        var min = planos.Count == 0 ? NivelImpacto.Bajo : planos.Count >= total ? NivelImpacto.Critico : vinc > 0 ? NivelImpacto.Alto : NivelImpacto.Medio;
        Cerrar(r, min);
        return r;
    }

    // ---------- Cargas de archivos del listado maestro de planos ----------
    public async Task<InformeImpacto> CargasArchivosPlanos(IReadOnlyCollection<int> ids)
    {
        var cargas = await _db.CargasArchivosPlanos.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto { Elemento = cargas.Count == 1 ? $"Carga de archivos \"{cargas[0].Origen}\" ({cargas[0].Fecha:dd/MM/yyyy HH:mm})" : $"{cargas.Count} cargas de archivos de planos" };
        var archivos = await _db.ArchivosPlanoMaestro.AsNoTracking().Where(a => a.CargaArchivosPlanosId != null && ids.Contains(a.CargaArchivosPlanosId.Value) && !a.Eliminado).ToListAsync();
        var idsArch = archivos.Select(a => a.Id).ToHashSet();
        var planos = archivos.Select(a => a.PlanoMaestroId).Distinct().ToList();
        var todos = await _db.ArchivosPlanoMaestro.AsNoTracking().Where(a => planos.Contains(a.PlanoMaestroId) && !a.Eliminado).ToListAsync();
        int pierdeActual = 0, sinArchivos = 0;
        foreach (var g in todos.GroupBy(a => a.PlanoMaestroId))
        {
            if (SigWeb.Data.Planos.ArchivosPlanos.Actuales(g).Any(a => idsArch.Contains(a.Id))) pierdeActual++;
            if (g.All(a => idsArch.Contains(a.Id))) sinArchivos++;
        }
        int vinc = await _db.ComponentesPlano.AsNoTracking().CountAsync(c => idsArch.Contains(c.ArchivoPlanoMaestroId));
        Agregar(r, "Listado maestro de planos", "archivos de esta(s) carga(s) que dejan de verse", archivos.Count, PesoAdjunto);
        Agregar(r, "Listado maestro de planos", "planos cuya versión actual es de esta carga: pasan a mostrar la anterior", pierdeActual, 4);
        Agregar(r, "Listado maestro de planos", "planos que quedan sin archivos (su código deja de ser vínculo)", sinArchivos, 3);
        Agregar(r, "Hoja de vida / Trazado", "componentes vinculados a estos archivos (pierden el enlace)", vinc, PesoPlano);
        Cerrar(r, pierdeActual > 0 || vinc > 0 ? NivelImpacto.Alto : archivos.Count > 0 ? NivelImpacto.Medio : NivelImpacto.Bajo);
        return r;
    }

    // ---------- Cargas de archivos del listado maestro de documentos ----------
    public async Task<InformeImpacto> CargasArchivosDocumentos(IReadOnlyCollection<int> ids)
    {
        var cargas = await _db.CargasArchivosDocumentos.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto
        {
            Elemento = cargas.Count == 1
                ? $"Carga de archivos \"{cargas[0].Origen}{(string.IsNullOrWhiteSpace(cargas[0].Descripcion) ? "" : " · " + cargas[0].Descripcion)}\" ({cargas[0].Fecha:dd/MM/yyyy HH:mm})"
                : $"{cargas.Count} cargas de archivos"
        };
        var archivos = await _db.VersionesDocumento.AsNoTracking()
            .Where(v => v.CargaArchivosDocumentosId != null && ids.Contains(v.CargaArchivosDocumentosId.Value)).ToListAsync();
        var docs = archivos.Select(v => v.DocumentoMaestroId).Distinct().ToList();
        // ¿Cuál es hoy el actual (vigente) y el editable de cada documento afectado?
        var todos = await _db.VersionesDocumento.AsNoTracking().Where(v => docs.Contains(v.DocumentoMaestroId)).ToListAsync();
        var idsCarga = archivos.Select(v => v.Id).ToHashSet();
        int pierdeActual = 0, pierdeEditable = 0, sinArchivos = 0;
        foreach (var g in todos.GroupBy(v => v.DocumentoMaestroId))
        {
            var orden = g.OrderByDescending(v => v.FechaCarga).ThenByDescending(v => v.Id).ToList();
            if (orden.FirstOrDefault(v => v.Clase == ClaseArchivoDocumento.Vigente) is { } a && idsCarga.Contains(a.Id)) pierdeActual++;
            if (orden.FirstOrDefault(v => v.Clase == ClaseArchivoDocumento.Editable) is { } e && idsCarga.Contains(e.Id)) pierdeEditable++;
            if (orden.All(v => idsCarga.Contains(v.Id))) sinArchivos++;
        }
        int cambios = await _db.ControlesCambioVersion.AsNoTracking().CountAsync(x => idsCarga.Contains(x.VersionDocumentoId));

        Agregar(r, "Listado maestro de documentos", "archivos de esta(s) carga(s) que dejan de verse", archivos.Count, PesoAdjunto);
        Agregar(r, "Listado maestro de documentos", "documentos cuya versión actual (vigente) es de esta carga: pasan a mostrar la anterior", pierdeActual, 4);
        Agregar(r, "Listado maestro de documentos", "documentos cuyo editable actual es de esta carga", pierdeEditable, 2);
        Agregar(r, "Listado maestro de documentos", "documentos que quedan sin ningún archivo (su código deja de ser vínculo)", sinArchivos, 3);
        Agregar(r, "Listado maestro de documentos", "filas de control de cambios de estas versiones", cambios, 1);
        Cerrar(r, pierdeActual > 0 ? NivelImpacto.Alto : archivos.Count > 0 ? NivelImpacto.Medio : NivelImpacto.Bajo);
        return r;
    }

    // ---------- Cargas (Excel) del listado maestro de documentos ----------
    public async Task<InformeImpacto> CargasDocumentos(IReadOnlyCollection<int> ids)
    {
        var cargas = await _db.CargasDocumentos.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto
        {
            Elemento = cargas.Count == 1
                ? $"Carga \"{cargas[0].NombreArchivo}{(string.IsNullOrWhiteSpace(cargas[0].Hoja) ? "" : " › " + cargas[0].Hoja)}\""
                : $"{cargas.Count} cargas de documentos"
        };
        var docs = await _db.DocumentosMaestro.AsNoTracking()
            .Where(d => d.CargaDocumentosId != null && ids.Contains(d.CargaDocumentosId.Value)).Select(d => d.Id).ToListAsync();
        var conVersion = await _db.VersionesDocumento.AsNoTracking().Where(v => docs.Contains(v.DocumentoMaestroId))
            .Select(v => v.DocumentoMaestroId).Distinct().CountAsync();
        int versiones = await _db.VersionesDocumento.AsNoTracking().CountAsync(v => docs.Contains(v.DocumentoMaestroId));
        // Filas que otra carga (posterior) actualizó por código: se irían igual con su carga de origen.
        int actualizadosPorOtras = cargas.Sum(c => c.FilasActualizadas);
        int totalDocs = await _db.DocumentosMaestro.AsNoTracking().CountAsync();

        Agregar(r, "Listado maestro de documentos", "documentos creados por esta(s) carga(s) que salen del listado", docs.Count, 1);
        Agregar(r, "Listado maestro de documentos", "documentos con versiones y archivos adjuntos que se irían con ellos", conVersion, 4);
        Agregar(r, "Listado maestro de documentos", "archivos de versiones que dejarían de verse", versiones, PesoAdjunto);
        Agregar(r, "Listado maestro de documentos", "filas de cargas anteriores que esta(s) carga(s) habían actualizado (esas no se borran, pero conservan los valores de esta carga)", actualizadosPorOtras, 0);

        // Vaciar el listado completo (o casi) nunca es "bajo".
        var min = docs.Count == 0 ? NivelImpacto.Bajo
            : docs.Count >= totalDocs ? NivelImpacto.Critico
            : conVersion > 0 ? NivelImpacto.Alto : NivelImpacto.Medio;
        Cerrar(r, min);
        return r;
    }

    public async Task<InformeImpacto> Tiro(int tiroId)
    {
        var t = await _db.Tiros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tiroId);
        var r = new InformeImpacto { Elemento = t is null ? "Tiro" : $"Tiro \"{t.Nombre}\"" };
        if (t is null) { Cerrar(r); return r; }
        int torres = await _db.ElementosTrazado.AsNoTracking().CountAsync(x => x.TiroId == tiroId && !x.Eliminado);
        int registros = await _db.RegistrosParametroTiro.AsNoTracking().CountAsync(x => x.TiroId == tiroId && x.Completado);
        Agregar(r, "Matriz PIE", "torres que quedarían sin tiro", torres, PesoComponente);
        Agregar(r, "Matriz PIE", "estados/avances del tiro que dejarían de verse", registros, PesoRegistroPie);
        Cerrar(r, registros > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    public async Task<InformeImpacto> Actividad(int actividadId)
    {
        var a = await _db.Actividades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actividadId);
        var r = new InformeImpacto { Elemento = a is null ? "Actividad" : $"Actividad \"{a.Nombre}\"" };
        if (a is null) { Cerrar(r); return r; }
        var parametros = await _db.ParametrosActividad.AsNoTracking().Where(p => p.ActividadId == actividadId).Select(p => p.Id).ToListAsync();
        int registros = await _db.RegistrosParametro.AsNoTracking().CountAsync(x => parametros.Contains(x.ParametroActividadId) && x.Completado);
        int vinculos = await _db.ComponenteActividades.AsNoTracking().CountAsync(x => x.ActividadId == actividadId);
        string donde = a.Ambito switch { AmbitoActividad.Torre => "Hoja de vida de torre / Matriz PIE", AmbitoActividad.Pata => "Ficha de pata (Trazado)", _ => "Tiros (Tendido)" };
        Agregar(r, "Actividades", "parámetros de la actividad que quedan ocultos", parametros.Count, 1);
        Agregar(r, donde, "estados/avances ya registrados que dejarían de verse", registros, PesoRegistroPie);
        Agregar(r, "Ficha de pata (Trazado)", "patas con esta actividad asignada", vinculos, PesoComponente);
        if (a.TipoEnsayoId != null)
        {
            int resultados = await _db.ResultadosEnsayo.AsNoTracking().CountAsync(x => x.TipoEnsayoId == a.TipoEnsayoId && !x.Eliminado);
            Agregar(r, "Ensayos", "resultados del ensayo enlazado a esta actividad (se conservan, pero Trazado deja de mostrar la actividad)", resultados, 1);
        }
        var min = registros > 0 || a.TipoEnsayoId != null ? NivelImpacto.Alto : NivelImpacto.Bajo;
        Cerrar(r, min);
        return r;
    }

    // ---------- Parámetros (sub actividades) ----------
    public async Task<InformeImpacto> Parametro(int parametroId)
    {
        var p = await _db.ParametrosActividad.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == parametroId);
        var r = new InformeImpacto { Elemento = p is null ? "Parámetro" : $"Parámetro \"{p.Nombre}\"" };
        if (p is null) { Cerrar(r); return r; }
        var a = await _db.Actividades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == p.ActividadId);
        var regs = await _db.RegistrosParametro.AsNoTracking().Where(x => x.ParametroActividadId == parametroId).Select(x => new { x.Id, x.Completado }).ToListAsync();
        var idsReg = regs.Select(x => x.Id).ToList();
        int adj = await _db.ArchivosRegistroParametro.AsNoTracking().CountAsync(x => idsReg.Contains(x.RegistroParametroId) && !x.Eliminado);
        string donde = a?.Ambito switch { AmbitoActividad.Pata => "Ficha de pata (Trazado) / Matriz PIE", AmbitoActividad.Tiro => "Tiros / Matriz PIE", _ => "Hoja de vida de torre / Matriz PIE" };
        Agregar(r, donde, "estados/avances ya registrados que dejarían de verse (se conservan)", regs.Count(x => x.Completado), PesoRegistroPie);
        Agregar(r, donde, "adjuntos de esos registros (se conservan)", adj, 1);
        Cerrar(r, regs.Any(x => x.Completado) ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    // ---------- Matriz de riesgos ----------
    public async Task<InformeImpacto> Riesgo(int riesgoId)
    {
        var x = await _db.Riesgos.AsNoTracking().FirstOrDefaultAsync(r => r.Id == riesgoId);
        var r2 = new InformeImpacto { Elemento = x is null ? "Riesgo" : $"Riesgo {x.Codigo} — {x.Descripcion}" };
        if (x is null) { Cerrar(r2); return r2; }
        var segs = await _db.SeguimientosRiesgo.AsNoTracking().Where(s => s.RiesgoId == riesgoId).Select(s => s.Id).ToListAsync();
        int hall = await _db.HallazgosRiesgo.AsNoTracking().CountAsync(h => segs.Contains(h.SeguimientoRiesgoId));
        int alc = await _db.AlcancesRiesgo.AsNoTracking().CountAsync(a => a.RiesgoId == riesgoId);
        Agregar(r2, "Riesgos", "seguimientos periódicos registrados a este riesgo", segs.Count, PesoRegistroPie);
        Agregar(r2, "Trazado (pestaña Hallazgos de la hoja de vida)", "hallazgos ya registrados que dejarían de verse", hall, PesoRegistroPie);
        Agregar(r2, "Riesgos", "torres o localizaciones a las que estaba asociado", alc, 1);
        int planes = await _db.PlanesAccion.AsNoTracking().CountAsync(p => p.RiesgoId == riesgoId || _db.RiesgosPlan.Any(r => r.PlanAccionId == p.Id && r.RiesgoId == riesgoId));
        Agregar(r2, "Planes de acción", "planes originados en este riesgo (se conservan, sin el riesgo visible)", planes, PesoRegistroPie);
        Cerrar(r2, hall > 0 ? NivelImpacto.Critico : segs.Count > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r2;
    }

    public async Task<InformeImpacto> SeguimientoRiesgo(int seguimientoId)
    {
        var s = await _db.SeguimientosRiesgo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == seguimientoId);
        var r = new InformeImpacto { Elemento = s is null ? "Seguimiento" : $"Seguimiento del {s.Fecha:dd/MM/yyyy}" };
        if (s is null) { Cerrar(r); return r; }
        int hall = await _db.HallazgosRiesgo.AsNoTracking().CountAsync(h => h.SeguimientoRiesgoId == seguimientoId);
        int arch = await _db.ArchivosRiesgo.AsNoTracking().CountAsync(a => a.SeguimientoRiesgoId == seguimientoId);
        Agregar(r, "Trazado (pestaña Hallazgos de la hoja de vida)", "hallazgos registrados en este seguimiento", hall, PesoRegistroPie);
        Agregar(r, "Riesgos", "soportes adjuntos al seguimiento", arch, PesoAdjunto);
        int planes = await _db.PlanesAccion.AsNoTracking().CountAsync(p => p.SeguimientoRiesgoId == seguimientoId);
        Agregar(r, "Planes de acción", "planes iniciados desde este seguimiento (se conservan)", planes, PesoRegistroPie);
        Cerrar(r, hall > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    public async Task<InformeImpacto> VersionMatrizRiesgos(int versionId)
    {
        var v = await _db.VersionesMatrizRiesgos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId);
        var r = new InformeImpacto { Elemento = v is null ? "Versión de la matriz" : $"Matriz de riesgos {v.Codigo}" };
        if (v is null) { Cerrar(r); return r; }
        var riesgos = await _db.Riesgos.AsNoTracking().Where(x => x.VersionMatrizRiesgosId == versionId).Select(x => x.Id).ToListAsync();
        var segs = await _db.SeguimientosRiesgo.AsNoTracking().Where(s => riesgos.Contains(s.RiesgoId)).Select(s => s.Id).ToListAsync();
        int hall = await _db.HallazgosRiesgo.AsNoTracking().CountAsync(h => segs.Contains(h.SeguimientoRiesgoId));
        Agregar(r, "Riesgos", "riesgos de esta revisión", riesgos.Count, PesoCatalogo);
        Agregar(r, "Riesgos", "seguimientos registrados contra esta revisión", segs.Count, PesoRegistroPie);
        Agregar(r, "Trazado (pestaña Hallazgos de la hoja de vida)", "hallazgos que dejarían de verse en las torres", hall, PesoRegistroPie);
        Cerrar(r, hall > 0 || segs.Count > 0 ? NivelImpacto.Critico : riesgos.Count > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    // ---------- Planes de acción ----------
    // ---------- Cargas (Excel) de Planes de acción ----------
    public async Task<InformeImpacto> CargasPlanesAccion(IReadOnlyCollection<int> ids)
    {
        var cargas = await _db.CargasPlanesAccion.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto { Elemento = cargas.Count == 1 ? $"Carga \"{cargas[0].NombreArchivo}\"" : $"{cargas.Count} cargas de planes de acción" };
        var planes = await _db.PlanesAccion.AsNoTracking().Where(p => p.CargaPlanesAccionId != null && ids.Contains(p.CargaPlanesAccionId.Value)).Select(p => p.Id).ToListAsync();
        var acciones = await _db.AccionesPlan.AsNoTracking().Where(a => planes.Contains(a.PlanAccionId)).ToListAsync();
        int lugares = await _db.LugaresPlan.AsNoTracking().CountAsync(l => planes.Contains(l.PlanAccionId));
        int conRiesgo = await _db.PlanesAccion.AsNoTracking().CountAsync(p => planes.Contains(p.Id) && p.RiesgoId != null);
        Agregar(r, "Planes de acción", "planes de esta(s) carga(s) que salen del listado", planes.Count, 1);
        Agregar(r, "Planes de acción", "acciones de esos planes", acciones.Count, 1);
        Agregar(r, "Planes de acción", "acciones cerradas (evidencia de cierre)", acciones.Count(a => a.Estado == EstadoAccionPlan.Cerrada), PesoRegistroPie);
        Agregar(r, "Trazado (pestaña Planes de acción de la hoja de vida)", "lugares donde se verían", lugares, 1);
        Agregar(r, "Riesgos", "planes ligados a un riesgo", conRiesgo, PesoRegistroPie);
        Cerrar(r, planes.Count == 0 ? NivelImpacto.Bajo : acciones.Any(a => a.Estado == EstadoAccionPlan.Cerrada) || conRiesgo > 0 ? NivelImpacto.Alto : NivelImpacto.Medio);
        return r;
    }

    public async Task<InformeImpacto> PlanAccion(int planId)
    {
        var p = await _db.PlanesAccion.AsNoTracking().FirstOrDefaultAsync(x => x.Id == planId);
        var r = new InformeImpacto { Elemento = p is null ? "Plan de acción" : $"Plan de acción {p.Codigo}" };
        if (p is null) { Cerrar(r); return r; }
        var acciones = await _db.AccionesPlan.AsNoTracking().Where(a => a.PlanAccionId == planId).ToListAsync();
        Agregar(r, "Planes de acción", "acciones propuestas del plan", acciones.Count, PesoRegistroPie);
        Agregar(r, "Planes de acción", "acciones cerradas (evidencia de cierre)", acciones.Count(a => a.Estado == EstadoAccionPlan.Cerrada), PesoRegistroPie);
        var idsAcc = acciones.Select(a => a.Id).ToList();
        Agregar(r, "Planes de acción", "anexos de productos", await _db.ArchivosAccionPlan.AsNoTracking().CountAsync(x => idsAcc.Contains(x.AccionPlanId)), PesoAdjunto);
        Agregar(r, "Trazado (pestaña Planes de acción de la hoja de vida)", "torres, elementos, localizaciones o tiros donde se vería", await _db.LugaresPlan.AsNoTracking().CountAsync(l => l.PlanAccionId == planId), 1);
        if (p.RiesgoId is not null)
            Agregar(r, "Riesgos", "trazabilidad de acciones en el detalle del riesgo", 1, PesoRegistroPie);
        Cerrar(r, acciones.Count > 0 ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    public async Task<InformeImpacto> AccionPlan(int accionId)
    {
        var a = await _db.AccionesPlan.AsNoTracking().FirstOrDefaultAsync(x => x.Id == accionId);
        var r = new InformeImpacto { Elemento = a is null ? "Acción" : $"Acción: {a.Descripcion}" };
        if (a is null) { Cerrar(r); return r; }
        Agregar(r, "Planes de acción", "estado del plan (se recalcula sin esta acción)", 1, 1);
        Agregar(r, "Planes de acción", "anexos del producto", await _db.ArchivosAccionPlan.AsNoTracking().CountAsync(x => x.AccionPlanId == accionId), PesoAdjunto);
        Cerrar(r, a.Estado == EstadoAccionPlan.Cerrada ? NivelImpacto.Medio : NivelImpacto.Bajo);
        return r;
    }

    // ---------- Grupos de planos ----------
    public async Task<InformeImpacto> GrupoPlano(int grupoId)
    {
        var g = await _db.GruposPlano.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == grupoId);
        var r = new InformeImpacto { Elemento = g is null ? "Grupo de planos" : $"Grupo de planos \"{g.Nombre}\"" };
        if (g is null) { Cerrar(r); return r; }
        var planos = await _db.PlanosMaestro.AsNoTracking().Where(p => p.GrupoPlanoId == grupoId && !p.Eliminado).Select(p => p.Id).ToListAsync();
        var archivos = await _db.ArchivosPlanoMaestro.AsNoTracking().Where(a => planos.Contains(a.PlanoMaestroId) && !a.Eliminado).Select(a => a.Id).ToListAsync();
        int vinc = await _db.ComponentesPlano.AsNoTracking().CountAsync(c => archivos.Contains(c.ArchivoPlanoMaestroId));
        Agregar(r, "Listado maestro de planos", "planos quedan sin clasificar (solo se verán en «Todos»)", planos.Count, 1);
        Agregar(r, "Hoja de vida / Trazado", "vínculos de planos con componentes (se conservan)", vinc, 1);
        Cerrar(r, planos.Count > 20 ? NivelImpacto.Alto : NivelImpacto.Bajo);
        return r;
    }

    // ---------- Planos maestros ----------
    public async Task<InformeImpacto> Plano(int planoId)
    {
        var p = await _db.PlanosMaestro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == planoId);
        var r = new InformeImpacto { Elemento = p is null ? "Plano" : $"Plano {p.Codigo}" };
        if (p is null) { Cerrar(r); return r; }
        var archivos = await _db.ArchivosPlanoMaestro.AsNoTracking().Where(a => a.PlanoMaestroId == planoId && !a.Eliminado).Select(a => a.Id).ToListAsync();
        int vinc = await _db.ComponentesPlano.AsNoTracking().CountAsync(c => archivos.Contains(c.ArchivoPlanoMaestroId));
        Agregar(r, "Planos maestros", "archivos del plano", archivos.Count, 1);
        Agregar(r, "Hoja de vida / Trazado", "componentes con este plano vinculado (su enlace al plano se pierde)", vinc, PesoPlano);
        Agregar(r, "Modelos 3D / Línea 3D", "las piezas del modelo que enlazan a este plano dejarán de abrirlo", 1, 2);
        Cerrar(r);
        return r;
    }

    public async Task<InformeImpacto> ArchivoPlano(int archivoId)
    {
        var a = await _db.ArchivosPlanoMaestro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == archivoId);
        var r = new InformeImpacto { Elemento = a is null ? "Archivo de plano" : $"Archivo \"{a.NombreOriginal}\"" };
        if (a is not null)
        {
            int vinc = await _db.ComponentesPlano.AsNoTracking().CountAsync(c => c.ArchivoPlanoMaestroId == a.Id);
            Agregar(r, "Hoja de vida / Trazado", "componentes vinculados a este archivo", vinc, PesoPlano);
            Agregar(r, "Modelos 3D / Línea 3D", "el hipervínculo del plano en las piezas del modelo", 1, 2);
        }
        Cerrar(r);
        return r;
    }

    // ---------- Materiales ----------
    public async Task<InformeImpacto> Material(int materialId)
    {
        var m = await _db.Materiales.AsNoTracking().FirstOrDefaultAsync(x => x.Id == materialId);
        var r = new InformeImpacto { Elemento = m is null ? "Material" : $"Material {(m.NombreMaterial != "" ? m.NombreMaterial : "#" + m.Id)}" };
        if (m is not null)
        {
            int ubic = await _db.UbicacionesMaterial.AsNoTracking().CountAsync(u => u.MaterialId == m.Id);
            Agregar(r, "Trazado (Hoja de vida › Materiales)", "torres, elementos o tiros donde deja de aparecer", ubic, 2);
            int adj = await _db.AdjuntosMaterial.AsNoTracking().CountAsync(a => a.MaterialId == m.Id);
            Agregar(r, "Materiales", "adjuntos (van con él a la Papelera)", adj, 1);
        }
        Cerrar(r);
        return r;
    }

    // ---------- Comunicados ----------
    public async Task<InformeImpacto> Comunicados(IReadOnlyCollection<int> ids)
    {
        var lista = await _db.Comunicados.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto { Elemento = lista.Count == 1 ? $"Comunicado {lista[0].Consecutivo}" : $"{lista.Count} comunicados" };
        var claves = lista.Select(c => SigWeb.Data.Comunicados.LogicaComunicados.Clave(c.Consecutivo)).Where(k => k.Length > 0).ToHashSet();
        int adj = await _db.AdjuntosComunicado.AsNoTracking().CountAsync(a => ids.Contains(a.ComunicadoId));
        // Otros comunicados que lo citan como su respuesta (quedarían con una respuesta que ya no está en el listado).
        var otros = await _db.Comunicados.AsNoTracking().Where(c => !ids.Contains(c.Id) && c.ConsecutivoRespuesta != null).Select(c => c.ConsecutivoRespuesta!).ToListAsync();
        int citado = otros.Count(o => claves.Contains(SigWeb.Data.Comunicados.LogicaComunicados.Clave(o)));
        Agregar(r, "Comunicados", "adjuntos y enlaces (van con él a la Papelera)", adj, PesoAdjunto);
        Agregar(r, "Comunicados", "comunicados que lo tienen como respuesta (pierden la fecha de respuesta)", citado, 2);
        Cerrar(r, citado > 0 ? NivelImpacto.Medio : NivelImpacto.Bajo);
        return r;
    }

    public async Task<InformeImpacto> CargasComunicados(IReadOnlyCollection<int> ids)
    {
        var cargas = await _db.CargasComunicados.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        var r = new InformeImpacto { Elemento = cargas.Count == 1 ? $"Carga \"{cargas[0].NombreArchivo}\"" : $"{cargas.Count} cargas de comunicados" };
        var idsCom = await _db.Comunicados.AsNoTracking().Where(c => c.CargaComunicadosId != null && ids.Contains(c.CargaComunicadosId.Value)).Select(c => c.Id).ToListAsync();
        int adj = await _db.AdjuntosComunicado.AsNoTracking().CountAsync(a => idsCom.Contains(a.ComunicadoId));
        int total = await _db.Comunicados.AsNoTracking().CountAsync();
        Agregar(r, "Comunicados", "comunicados de esta(s) carga(s) que salen del listado", idsCom.Count, 1);
        Agregar(r, "Comunicados", "adjuntos y enlaces de esos comunicados", adj, PesoAdjunto);
        Cerrar(r, idsCom.Count == 0 ? NivelImpacto.Bajo : idsCom.Count >= total ? NivelImpacto.Critico : adj > 0 ? NivelImpacto.Alto : NivelImpacto.Medio);
        return r;
    }

    // ---------- Indicadores ----------
    public async Task<InformeImpacto> Indicadores(int id)
    {
        var ind = await _db.Indicadores.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        var r = new InformeImpacto { Elemento = $"Indicador \"{ind?.Nombre}\"" };
        var idsMed = await _db.MedicionesIndicador.AsNoTracking().Where(m => m.IndicadorId == id).Select(m => m.Id).ToListAsync();
        int congeladas = await _db.MedicionesIndicador.AsNoTracking().CountAsync(m => m.IndicadorId == id && m.Congelado);
        int soportes = await _db.ArchivosMedicionIndicador.AsNoTracking().CountAsync(a => idsMed.Contains(a.MedicionIndicadorId));
        Agregar(r, "Indicadores", "mediciones guardadas (digitadas o congeladas; van con él a la Papelera)", idsMed.Count, 2);
        Agregar(r, "Indicadores", "soportes adjuntos de esas mediciones", soportes, PesoAdjunto);
        Cerrar(r, congeladas > 0 || soportes > 0 ? NivelImpacto.Alto : idsMed.Count > 0 ? NivelImpacto.Medio : NivelImpacto.Bajo);
        return r;
    }
}
