using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera para los "datos adicionales de torres" (CampoExtraTorre): tanto
// quitar un dato suelto como eliminar una carga completa desde
// la página Tabla de torres (todos los campos que compartan un mismo Origen)
// solo marcan Eliminado = true, así que ambos casos llegan aquí igual.
public class ProveedorPapeleraCamposExtraTorre : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraCamposExtraTorre(AppDbContext db)
    {
        _db = db;
    }

    public string Modulo => "Datos adicionales de torres";

    public List<RegistroPapelera> Listar()
    {
        var torres = _db.ElementosTrazado.AsNoTracking().ToDictionary(t => t.Id, t => t.Codigo);

        return _db.CamposExtraTorre.AsNoTracking()
            .Where(c => c.Eliminado)
            .OrderByDescending(c => c.FechaEliminacion)
            .ToList()
            .Select(c => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = c.Id,
                Titulo = (torres.TryGetValue(c.ElementoTrazadoId, out var codigo) ? $"Torre {codigo}" : "Torre") + $" — {c.Clave}",
                Subtitulo = (string.IsNullOrWhiteSpace(c.Valor) ? "(sin valor)" : c.Valor) + " · " + c.Origen,
                FechaEliminacion = c.FechaEliminacion
            })
            .ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var avisos = new List<AvisoRestauracion>();
        var c = _db.CamposExtraTorre.AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (c is null) return avisos;
        var torre = _db.ElementosTrazado.AsNoTracking().FirstOrDefault(t => t.Id == c.ElementoTrazadoId);
        var codigo = torre?.Codigo ?? "?";

        if (torre is null || torre.Eliminado)
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "grave",
                Mensaje = $"La torre {codigo} está en la Papelera (su referencia en el mapa fue eliminada): el dato volvería, pero sin torre visible a la que mostrarse.",
                Solucion = "Restaura primero la torre en la Papelera (módulo 'Torres (referencia en el mapa)'). Si ahora la torre existe con otro código, renómbrala o unifícala desde Trazado para que coincida.",
                Menus = new() { "Trazado (ficha de la torre)", "Tabla de torres", "Línea 3D" }
            });

        var actual = _db.CamposExtraTorre.AsNoTracking()
            .FirstOrDefault(x => x.ElementoTrazadoId == c.ElementoTrazadoId && x.Clave == c.Clave && !x.Eliminado && x.Id != c.Id);
        if (actual is not null && actual.Valor.Trim() != c.Valor.Trim())
        {
            var menus = new List<string> { "Trazado (ficha de la torre)", "Tabla de torres" };
            var k = c.Clave.ToLowerInvariant();
            bool esTipo = k.Contains("cuerpo") || (k.Contains("tipo") && !k.Contains("cimenta") && !k.Contains("suelo"));
            if (esTipo) { menus.Add("Línea 3D"); menus.Add("Modelos 3D"); }
            avisos.Add(new AvisoRestauracion
            {
                Nivel = esTipo ? "grave" : "atencion",
                Mensaje = $"La torre {codigo} hoy tiene \"{actual.Valor}\" en '{c.Clave}'. Al restaurar quedará \"{c.Valor}\" y el valor actual irá a la Papelera."
                    + (esTipo ? " Este campo define el tipo de torre: cambia el modelo que se dibuja en Línea 3D." : ""),
                Solucion = "Si prefieres conservar el valor actual, cancela. Si necesitas ambos, restaura y luego vuelve a poner el valor actual desde 'Tabla de torres' (✎ Editar).",
                Menus = menus
            });
        }

        var cfg = _db.ConfiguracionesCampoExtraTorre.AsNoTracking().FirstOrDefault(x => x.Clave == c.Clave);
        if (cfg is not null && !cfg.Activo)
            avisos.Add(new AvisoRestauracion
            {
                Nivel = "info",
                Mensaje = $"La columna '{c.Clave}' está oculta: el dato se restaura pero no se verá.",
                Solucion = "Actívala en Tabla de torres › ⚙ Columnas.",
                Menus = new() { "Tabla de torres", "Trazado (ficha de la torre)" }
            });
        return avisos;
    }

    public void Restaurar(int id)
    {
        var c = _db.CamposExtraTorre.Find(id);
        if (c is not null)
        {
            c.Eliminado = false;
            c.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.CamposExtraTorre.Find(id);
        if (c is not null)
        {
            _db.CamposExtraTorre.Remove(c);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.CamposExtraTorre
            .Where(c => c.Eliminado && c.FechaEliminacion != null && c.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        _db.CamposExtraTorre.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }
}
