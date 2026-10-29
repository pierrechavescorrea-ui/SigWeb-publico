using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera para la referencia GRÁFICA de una torre (el punto en el mapa).
// A propósito, "eliminar" aquí nunca borra sus Componentes, checklist ni
// materiales ya cargados: solo saca la torre del mapa. Si luego se carga
// otro KMZ/Excel con el mismo código (o uno parecido, vía el flujo de
// "¿es la misma torre?"), esos datos se recuperan automáticamente sobre
// la nueva referencia. El borrado físico definitivo (aquí, desde
// EliminarDefinitivo) sí se lleva todo con ella, en cascada.
public class ProveedorPapeleraElementosTrazado : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraElementosTrazado(AppDbContext db)
    {
        _db = db;
    }

    public string Modulo => "Torres (referencia en el mapa)";

    public List<RegistroPapelera> Listar()
    {
        var capas = _db.Capas.AsNoTracking().ToDictionary(c => c.Id, c => c.Nombre);

        return _db.ElementosTrazado.AsNoTracking()
            .Where(e => e.Eliminado)
            .OrderByDescending(e => e.FechaEliminacion)
            .ToList()
            .Select(e => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = e.Id,
                Titulo = e.Codigo,
                Subtitulo = e.CapaTrazadoId != null && capas.TryGetValue(e.CapaTrazadoId.Value, out var nombreCapa)
                    ? $"Capa: {nombreCapa}"
                    : "",
                FechaEliminacion = e.FechaEliminacion
            })
            .ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var e = _db.ElementosTrazado.AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (e is null) return avisos;
        var cod = e.Codigo.Trim().ToUpperInvariant();
        var otras = _db.ElementosTrazado.AsNoTracking().Where(x => !x.Eliminado && x.Id != id).ToList()
            .Where(x => x.Codigo.Trim().ToUpperInvariant() == cod).ToList();
        foreach (var o in otras)
        {
            // distancia aproximada en metros entre las dos ubicaciones
            var dLat = (o.Latitud - e.Latitud) * 111320.0;
            var dLon = (o.Longitud - e.Longitud) * 111320.0 * Math.Cos(e.Latitud * Math.PI / 180.0);
            var dist = Math.Sqrt(dLat * dLat + dLon * dLon);
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "grave",
                Mensaje = $"Ya existe otra torre {e.Codigo} en el mapa" + (dist > 1 ? $", a {dist:0} m de la que se restauraría (la coordenada cambió)." : " en la misma ubicación.")
                    + " Quedarían dos torres con el mismo código: los datos, la hoja de vida y la Matriz PIE se asignan por código y se mezclarían.",
                Solucion = "Antes de restaurar, decide con cuál ubicación te quedas: si es la nueva, no restaures (déjala en la Papelera); si es la anterior, elimina o renombra la actual. Si son la misma torre, usa 'Unificar torres' en Trazado.",
                Menus = new() { "Trazado (mapa)", "Tabla de torres", "Matriz PIE", "Hoja de vida", "Línea 3D" }
            });
        }
        return avisos;
    }

    public void Restaurar(int id)
    {
        var e = _db.ElementosTrazado.Find(id);
        if (e is not null)
        {
            e.Eliminado = false;
            e.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var e = _db.ElementosTrazado.Find(id);
        if (e is not null)
        {
            // Esto sí borra en cascada sus Componentes, checklist y adjuntos
            // (los Materiales vinculados quedan sin ubicación, no se borran).
            _db.ElementosTrazado.Remove(e);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.ElementosTrazado
            .Where(e => e.Eliminado && e.FechaEliminacion != null && e.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        _db.ElementosTrazado.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}