using System.Collections.Concurrent;

namespace SigWeb.Data.Dossier;

// La pantalla (circuito Blazor) deja aquí la solicitud y el navegador la descarga por
// GET /api/dossier/{token}: así el ZIP sale por HTTP normal (streaming, barra de descarga del
// navegador, cancelable) y no por el circuito, que se colgaría con archivos grandes.
public sealed class AlmacenDossier
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, (SolicitudDossier S, DateTime Vence)> pendientes = new();

    public string Guardar(SolicitudDossier s)
    {
        var ahora = DateTime.UtcNow;
        foreach (var kv in pendientes) if (kv.Value.Vence < ahora) pendientes.TryRemove(kv.Key, out _);
        var token = Guid.NewGuid().ToString("N");
        pendientes[token] = (s, ahora + Vigencia);
        return token;
    }

    // No se borra al usarla: el navegador puede reintentar la descarga mientras siga vigente.
    public SolicitudDossier? Obtener(string token) =>
        pendientes.TryGetValue(token, out var p) && p.Vence >= DateTime.UtcNow ? p.S : null;
}
