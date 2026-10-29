using System.IO.Compression;
using SharpKml.Dom;
using SharpKml.Engine;

namespace SigWeb.Data.Trazado;

// Un punto tal como viene del KMZ, antes de convertirlo en ElementoTrazado.
public class PuntoImportado
{
    public string Codigo { get; set; } = "";
    public string? Nombre { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
}

// Lee un KMZ (que no es más que un .zip con un .kml adentro) y devuelve
// cada Placemark de tipo punto que encuentre. No toca la base de datos:
// solo interpreta el archivo. Quien llama decide qué hacer con los puntos
// (en este proyecto: Trazado.razor los convierte en torres + sus 4 patas).
public static class LectorKmz
{
    public static List<PuntoImportado> Leer(Stream kmzStream)
    {
        var resultado = new List<PuntoImportado>();

        using var archivo = new ZipArchive(kmzStream, ZipArchiveMode.Read);
        var entradaKml = archivo.Entries.FirstOrDefault(e => e.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase));
        if (entradaKml is null)
        {
            throw new InvalidOperationException("El archivo KMZ no contiene ningún .kml adentro.");
        }

        using var streamKml = entradaKml.Open();
        var kml = KmlFile.Load(streamKml);

        var placemarks = kml.Root.Flatten().OfType<Placemark>();

        int contador = 1;
        foreach (var p in placemarks)
        {
            if (p.Geometry is Point punto && punto.Coordinate is not null)
            {
                var tieneNombre = !string.IsNullOrWhiteSpace(p.Name);
                var codigo = tieneNombre ? p.Name!.Trim() : $"T-{contador}";

                resultado.Add(new PuntoImportado
                {
                    Codigo = codigo,
                    Nombre = tieneNombre ? p.Name!.Trim() : codigo,
                    Latitud = punto.Coordinate.Latitude,
                    Longitud = punto.Coordinate.Longitude
                });
                contador++;
            }
        }

        return resultado;
    }
}