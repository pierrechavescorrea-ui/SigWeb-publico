using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Planes;

public class PlanesAccionServicio
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopes;
    public PlanesAccionServicio(AppDbContext db, IServiceScopeFactory scopes) { _db = db; _scopes = scopes; }

    // Paso de arranque: el lugar único de los planes anteriores
    // (PlanAccion.ItemCatalogoId) pasa a la lista de lugares. Seguro de repetir.
    public static void MigrarLugarUnico(AppDbContext db)
    {
        var planes = db.PlanesAccion.IgnoreQueryFilters().Where(p => p.ItemCatalogoId != null).ToList();
        if (planes.Count == 0) return;
        foreach (var p in planes)
        {
            if (!db.LugaresPlan.Any(l => l.PlanAccionId == p.Id && l.ItemCatalogoId == p.ItemCatalogoId))
                db.LugaresPlan.Add(new LugarPlan { PlanAccionId = p.Id, ItemCatalogoId = p.ItemCatalogoId });
            p.ItemCatalogoId = null;
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Paso de arranque: el riesgo único de cada plan (PlanAccion.RiesgoId) entra a su lista de
    // riesgos (RiesgoPlan). Seguro de repetir.
    public static void MigrarRiesgoUnico(AppDbContext db)
    {
        var conRiesgo = db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().Where(p => p.RiesgoId != null)
            .Select(p => new { p.Id, RiesgoId = p.RiesgoId!.Value }).ToList();
        if (conRiesgo.Count == 0) return;
        var ya = db.RiesgosPlan.AsNoTracking().Select(r => new { r.PlanAccionId, r.RiesgoId }).ToList()
            .Select(r => (r.PlanAccionId, r.RiesgoId)).ToHashSet();
        var existen = db.Riesgos.IgnoreQueryFilters().AsNoTracking().Select(r => r.Id).ToHashSet();
        foreach (var p in conRiesgo.Where(p => !ya.Contains((p.Id, p.RiesgoId)) && existen.Contains(p.RiesgoId)))
            db.RiesgosPlan.Add(new RiesgoPlan { PlanAccionId = p.Id, RiesgoId = p.RiesgoId, Orden = 0 });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    // Riesgos de cada plan (lista ordenada; incluye PlanAccion.RiesgoId aunque aún no esté en RiesgoPlan).
    public static Dictionary<int, List<int>> RiesgosDePlanes(AppDbContext db, IEnumerable<PlanAccion> planes)
    {
        var lista = planes.ToList();
        var ids = lista.Select(p => p.Id).ToList();
        var rel = db.RiesgosPlan.AsNoTracking().Where(r => ids.Contains(r.PlanAccionId)).OrderBy(r => r.Orden).ThenBy(r => r.Id).ToList();
        var d = new Dictionary<int, List<int>>();
        foreach (var p in lista)
        {
            var l = rel.Where(r => r.PlanAccionId == p.Id).Select(r => r.RiesgoId).ToList();
            if (p.RiesgoId is int r0 && !l.Contains(r0)) l.Insert(0, r0);
            if (l.Count > 0) d[p.Id] = l;
        }
        return d;
    }

    // ---------- Edición de un plan (página Planes de acción y formulario de seguimiento) ----------
    public class ProductoEditable
    {
        public ProductoAccion Producto { get; set; } = new();
        public List<ArchivoAccionPlan> Anexos { get; set; } = new();   // Id 0 = subido, aún sin registrar
    }

    public class AccionEditable
    {
        public AccionPlan Accion { get; set; } = new();
        public List<string> Responsables { get; set; } = new();
        public List<ProductoEditable> Productos { get; set; } = new();
    }

    public class PlanEditable
    {
        public PlanAccion Plan { get; set; } = new();
        public List<string> Procesos { get; set; } = new();
        public List<LugarPlan> Lugares { get; set; } = new();
        // Riesgos asociados (Id), en orden; el primero queda también en Plan.RiesgoId.
        public List<int> Riesgos { get; set; } = new();
        public List<AccionEditable> Acciones { get; set; } = new();
        public List<ArchivoAccionPlan> AnexosQuitados { get; set; } = new();
    }

    public PlanEditable? Cargar(int planId)
    {
        var p = _db.PlanesAccion.AsNoTracking().FirstOrDefault(x => x.Id == planId);
        if (p is null) return null;
        var acciones = _db.AccionesPlan.AsNoTracking().Where(a => a.PlanAccionId == planId).OrderBy(a => a.Orden).ThenBy(a => a.Id).ToList();
        var ids = acciones.Select(a => a.Id).ToList();
        // Los soportes de seguimientos no son anexos de producto.
        var anexos = _db.ArchivosAccionPlan.AsNoTracking().Where(x => ids.Contains(x.AccionPlanId) && x.SeguimientoAccionId == null).OrderBy(x => x.Id).ToList();
        var productos = _db.ProductosAccion.AsNoTracking().Where(x => ids.Contains(x.AccionPlanId)).OrderBy(x => x.Orden).ThenBy(x => x.Id).ToList();
        return new PlanEditable
        {
            Plan = p,
            Procesos = Lista(p.Proceso),
            Riesgos = RiesgosDePlanes(_db, new[] { p }).TryGetValue(p.Id, out var rs) ? rs : new(),
            Lugares = _db.LugaresPlan.AsNoTracking().Where(l => l.PlanAccionId == planId).ToList()
                .Select(l => new LugarPlan { ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId }).ToList(),
            Acciones = acciones.Select(a => new AccionEditable
            {
                Accion = a, Responsables = Lista(a.Responsable), Productos = ProductosDe(a, productos, anexos)
            }).ToList()
        };
    }

    // Productos de una acción con sus anexos. Acciones anteriores a los productos: su texto "Producto"
    // (una línea por producto) y sus anexos sueltos quedan en el primero; se registran al guardar.
    private static List<ProductoEditable> ProductosDe(AccionPlan a, List<ProductoAccion> productos, List<ArchivoAccionPlan> anexos)
    {
        var suyos = productos.Where(p => p.AccionPlanId == a.Id).ToList();
        var sueltos = anexos.Where(x => x.AccionPlanId == a.Id && (x.ProductoAccionId is null || suyos.All(p => p.Id != x.ProductoAccionId))).ToList();
        var l = suyos.Select(p => new ProductoEditable { Producto = p, Anexos = anexos.Where(x => x.ProductoAccionId == p.Id).ToList() }).ToList();
        if (l.Count == 0)
        {
            var textos = Lista(a.Producto);
            l = (textos.Count == 0 && sueltos.Count > 0 ? new List<string> { "" } : textos)
                .Select(t => new ProductoEditable { Producto = new ProductoAccion { Descripcion = t } }).ToList();
        }
        if (sueltos.Count > 0) l[0].Anexos.AddRange(sueltos);
        return l;
    }

    private static List<string> Lista(string? t) =>
        (t ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // PA-001, PA-002… sin repetir los de la Papelera ni los ya reservados en pantalla.
    public string SiguienteCodigo(IEnumerable<string>? reservados = null)
    {
        var usados = _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().Select(p => p.Codigo).ToList();
        if (reservados is not null) usados.AddRange(reservados);
        var max = usados.Select(c => System.Text.RegularExpressions.Regex.Match(c ?? "", @"^PA-(\d+)$"))
                        .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        return "PA-" + (max + 1).ToString("000");
    }

    // Guarda el plan con sus lugares, acciones y anexos. Lanza InvalidOperationException
    // con un mensaje para el usuario si falta algo. Devuelve el Id del plan.
    public async Task<int> GuardarAsync(PlanEditable e)
    {
        var f = e.Plan;
        var codigo = (f.Codigo ?? "").Trim();
        if (codigo.Length == 0) throw new InvalidOperationException("El plan necesita un código.");
        if (string.IsNullOrWhiteSpace(f.DescripcionHallazgo)) throw new InvalidOperationException($"Describe el hallazgo del plan {codigo}.");
        if (await _db.PlanesAccion.IgnoreQueryFilters().AnyAsync(p => p.Id != f.Id && p.Codigo == codigo))
            throw new InvalidOperationException($"Ya existe un plan con el código \"{codigo}\" (puede estar en la Papelera).");

        PlanAccion ent;
        if (f.Id == 0) { ent = new PlanAccion(); _db.PlanesAccion.Add(ent); }
        else ent = await _db.PlanesAccion.FirstAsync(p => p.Id == f.Id);
        ent.Codigo = codigo;
        ent.Fecha = f.Fecha;
        ent.Proceso = string.Join("\n", e.Procesos);
        ent.ItemCatalogoId = null;
        ent.OrigenHallazgo = (f.OrigenHallazgo ?? "").Trim();
        ent.TipoHallazgo = (f.TipoHallazgo ?? "").Trim();
        ent.DescripcionHallazgo = f.DescripcionHallazgo.Trim();
        ent.AnalisisCausa = f.AnalisisCausa ?? "";
        ent.ReportoNombre = (f.ReportoNombre ?? "").Trim();
        ent.ReportoCargo = (f.ReportoCargo ?? "").Trim();
        ent.ReportoEmpresa = (f.ReportoEmpresa ?? "").Trim();
        // Riesgos: la lista manda; si llega vacía pero el plan trae RiesgoId (nace de un riesgo o seguimiento), ese.
        var riesgos = e.Riesgos.Distinct().ToList();
        if (riesgos.Count == 0 && f.RiesgoId is int r0 && e.Riesgos.Count == 0 && f.Id == 0) riesgos.Add(r0);
        ent.RiesgoId = riesgos.Contains(f.RiesgoId ?? -1) ? f.RiesgoId : riesgos.Count > 0 ? riesgos[0] : null;
        ent.SeguimientoRiesgoId = ent.RiesgoId is null ? null : f.SeguimientoRiesgoId;
        await _db.SaveChangesAsync();

        _db.RiesgosPlan.RemoveRange(await _db.RiesgosPlan.Where(r => r.PlanAccionId == ent.Id).ToListAsync());
        for (int i = 0; i < riesgos.Count; i++)
            _db.RiesgosPlan.Add(new RiesgoPlan { PlanAccionId = ent.Id, RiesgoId = riesgos[i], Orden = i });

        _db.LugaresPlan.RemoveRange(await _db.LugaresPlan.Where(l => l.PlanAccionId == ent.Id).ToListAsync());
        foreach (var l in e.Lugares)
            _db.LugaresPlan.Add(new LugarPlan { PlanAccionId = ent.Id, ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId });

        var existentes = await _db.AccionesPlan.Where(a => a.PlanAccionId == ent.Id).ToDictionaryAsync(a => a.Id);
        var destinos = new List<(AccionEditable Origen, AccionPlan Destino)>();
        var orden = 0;
        foreach (var a in e.Acciones.Where(a => !string.IsNullOrWhiteSpace(a.Accion.Descripcion)))
        {
            orden++;
            AccionPlan dest;
            if (a.Accion.Id > 0 && existentes.TryGetValue(a.Accion.Id, out var e0)) dest = e0;
            else { dest = new AccionPlan { PlanAccionId = ent.Id }; _db.AccionesPlan.Add(dest); }
            dest.Orden = orden;
            dest.Tipo = a.Accion.Tipo;
            dest.Descripcion = a.Accion.Descripcion.Trim();
            dest.Fecha = a.Accion.Fecha;
            dest.Producto = string.Join("\n", a.Productos.Select(p => (p.Producto.Descripcion ?? "").Trim()).Where(t => t.Length > 0));
            dest.Responsable = string.Join("\n", a.Responsables);
            dest.Estado = a.Accion.Estado;
            destinos.Add((a, dest));
        }
        await _db.SaveChangesAsync();

        // Productos: se conservan los que siguen, se crean los nuevos y se borran los quitados
        // (sus anexos van a la Papelera como los demás anexos quitados).
        var idsAcciones = destinos.Select(d => d.Destino.Id).ToList();
        var prodExist = await _db.ProductosAccion.Where(p => idsAcciones.Contains(p.AccionPlanId)).ToDictionaryAsync(p => p.Id);
        var prodQuedan = new HashSet<int>();
        var prodDestino = new List<(ProductoEditable Origen, ProductoAccion Destino, int AccionId)>();
        foreach (var (a, dest) in destinos)
        {
            int op = 0;
            foreach (var pe in a.Productos.Where(p => !string.IsNullOrWhiteSpace(p.Producto.Descripcion) || p.Anexos.Count > 0))
            {
                ProductoAccion pd;
                if (pe.Producto.Id > 0 && prodExist.TryGetValue(pe.Producto.Id, out var p0)) pd = p0;
                else { pd = new ProductoAccion { AccionPlanId = dest.Id }; _db.ProductosAccion.Add(pd); }
                pd.Orden = ++op;
                pd.Descripcion = (pe.Producto.Descripcion ?? "").Trim();
                if (pd.Id > 0) prodQuedan.Add(pd.Id);
                prodDestino.Add((pe, pd, dest.Id));
            }
        }
        foreach (var p in prodExist.Values.Where(p => !prodQuedan.Contains(p.Id))) _db.ProductosAccion.Remove(p);
        await _db.SaveChangesAsync();

        var idsAnexosQuedan = new HashSet<int>();
        foreach (var (pe, pd, accionId) in prodDestino)
            foreach (var x in pe.Anexos)
            {
                if (x.Id == 0) _db.ArchivosAccionPlan.Add(new ArchivoAccionPlan { AccionPlanId = accionId, ProductoAccionId = pd.Id, NombreOriginal = x.NombreOriginal, RutaArchivo = x.RutaArchivo });
                else idsAnexosQuedan.Add(x.Id);
            }
        // Anexos ya guardados: quedan en su producto (los que venían sueltos se asignan ahora).
        var anexosExist = await _db.ArchivosAccionPlan.Where(x => idsAcciones.Contains(x.AccionPlanId) && idsAnexosQuedan.Contains(x.Id)).ToListAsync();
        foreach (var (pe, pd, _) in prodDestino)
            foreach (var x in anexosExist.Where(x => pe.Anexos.Any(y => y.Id == x.Id))) x.ProductoAccionId = pd.Id;
        var idsQuitar = e.AnexosQuitados.Select(x => x.Id).ToList();
        var ahora = DateTime.Now;
        foreach (var x in await _db.ArchivosAccionPlan.Where(x => idsQuitar.Contains(x.Id)).ToListAsync())
        { x.Eliminado = true; x.FechaEliminacion = ahora; }
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return ent.Id;
    }

    // Paso de arranque: el lugar único de cada hallazgo (campos de
    // HallazgoRiesgo) pasa a la lista de lugares del hallazgo. Seguro de repetir.
    public static void MigrarLugarHallazgos(AppDbContext db)
    {
        var hs = db.HallazgosRiesgo.IgnoreQueryFilters()
            .Where(h => h.ElementoTrazadoId != null || h.ComponenteId != null || h.ItemCatalogoId != null || h.TiroId != null).ToList();
        if (hs.Count == 0) return;
        foreach (var h in hs)
        {
            db.LugaresHallazgo.Add(new LugarHallazgo { HallazgoRiesgoId = h.Id, ElementoTrazadoId = h.ElementoTrazadoId, ComponenteId = h.ComponenteId, ItemCatalogoId = h.ItemCatalogoId, TiroId = h.TiroId });
            h.ElementoTrazadoId = null; h.ComponenteId = null; h.ItemCatalogoId = null; h.TiroId = null;
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    public class PlanTorre
    {
        public PlanAccion Plan { get; set; } = default!;
        public List<string> Donde { get; set; } = new();
        public List<AccionPlan> Acciones { get; set; } = new();
        public string? RiesgoCodigo { get; set; }
        public string? RiesgoDescripcion { get; set; }
        public Dictionary<int, List<ArchivoAccionPlan>> Anexos { get; set; } = new();   // por acción
        // Por acción: cada producto con sus anexos (en el orden del plan).
        public Dictionary<int, List<(string Producto, List<ArchivoAccionPlan> Anexos)>> Productos { get; set; } = new();
        public EstadoAccionPlan Estado => EscalaAccion.EstadoPlan(Acciones);
    }

    // Planes ligados a una torre: a ella, a una de sus patas o elementos, a
    // una localización del catálogo amarrada a ella, o al tiro al que pertenece.
    // Con su propio DbContext, por la misma razón que HallazgosDeTorreAsync.
    public async Task<List<PlanTorre>> PlanesDeTorreAsync(int elementoTrazadoId)
    {
        using var scope = _scopes.CreateScope();
        var _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var torre = await _db.ElementosTrazado.AsNoTracking().FirstOrDefaultAsync(t => t.Id == elementoTrazadoId);
        if (torre is null) return new();
        var comps = await _db.Componentes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.ElementoTrazadoId == elementoTrazadoId).ToDictionaryAsync(c => c.Id, c => c.Nombre);
        var idsComp = comps.Keys.ToList();
        var locs = await _db.Catalogos.AsNoTracking()
            .Where(i => i.ElementoTrazadoId == elementoTrazadoId).ToDictionaryAsync(i => i.Id, i => i.Nombre);
        var idsLoc = locs.Keys.ToList();
        var tiro = torre.TiroId is int tid ? await _db.Tiros.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid) : null;

        var lugares = await _db.LugaresPlan.AsNoTracking()
            .Where(l => l.ElementoTrazadoId == elementoTrazadoId
                     || (l.ComponenteId != null && idsComp.Contains(l.ComponenteId.Value))
                     || (l.ItemCatalogoId != null && idsLoc.Contains(l.ItemCatalogoId.Value))
                     || (tiro != null && l.TiroId == tiro.Id))
            .ToListAsync();
        var idsPlan = lugares.Select(l => l.PlanAccionId).Distinct().ToList();
        var planes = await _db.PlanesAccion.AsNoTracking().Where(p => idsPlan.Contains(p.Id))
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).ToListAsync();
        var acciones = await _db.AccionesPlan.AsNoTracking().Where(a => idsPlan.Contains(a.PlanAccionId)).ToListAsync();
        var riesgosPlan = RiesgosDePlanes(_db, planes);
        var idsRiesgo = riesgosPlan.Values.SelectMany(l => l).Distinct().ToList();
        var riesgos = await _db.Riesgos.IgnoreQueryFilters().AsNoTracking().Where(r => idsRiesgo.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => new { r.Codigo, r.Descripcion });
        var idsAcc = acciones.Select(a => a.Id).ToList();
        var anexos = await _db.ArchivosAccionPlan.AsNoTracking().Where(x => idsAcc.Contains(x.AccionPlanId) && x.SeguimientoAccionId == null).OrderBy(x => x.Id).ToListAsync();
        var productos = await _db.ProductosAccion.AsNoTracking().Where(x => idsAcc.Contains(x.AccionPlanId)).OrderBy(x => x.Orden).ThenBy(x => x.Id).ToListAsync();
        var porAccion = acciones.ToDictionary(a => a.Id, a => ProductosDe(a, productos, anexos)
            .Select(pe => (pe.Producto.Descripcion, pe.Anexos)).ToList());

        string Texto(LugarPlan l) =>
            l.ElementoTrazadoId is not null ? "Torre"
            : l.ComponenteId is int c ? (comps.TryGetValue(c, out var n) ? n : "Elemento")
            : l.ItemCatalogoId is int i ? (locs.TryGetValue(i, out var nl) ? nl : "Localización")
            : $"Tiro {tiro?.Nombre}";

        return planes.Select(p => new PlanTorre
        {
            Plan = p,
            Donde = lugares.Where(l => l.PlanAccionId == p.Id).Select(Texto).Distinct().ToList(),
            Acciones = acciones.Where(a => a.PlanAccionId == p.Id).OrderBy(a => a.Orden).ToList(),
            RiesgoCodigo = riesgosPlan.TryGetValue(p.Id, out var lr) && lr.Count > 0
                ? string.Join(", ", lr.Where(riesgos.ContainsKey).Select(r => riesgos[r].Codigo)) : null,
            RiesgoDescripcion = riesgosPlan.TryGetValue(p.Id, out var lr2) && lr2.Count > 0
                ? string.Join(" · ", lr2.Where(riesgos.ContainsKey).Select(r => riesgos[r].Descripcion)) : null,
            Anexos = anexos.Where(x => acciones.Any(a => a.Id == x.AccionPlanId && a.PlanAccionId == p.Id))
                .GroupBy(x => x.AccionPlanId).ToDictionary(g => g.Key, g => g.ToList()),
            Productos = acciones.Where(a => a.PlanAccionId == p.Id).ToDictionary(a => a.Id, a => porAccion[a.Id])
        }).ToList();
    }
}
