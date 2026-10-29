using Microsoft.AspNetCore.Components.Forms;

namespace SigWeb.Data.Planes;

// Guarda un anexo subido (documento, foto…) en wwwroot/uploads/<carpeta>
// con nombre único, conservando la extensión original.
public static class AlmacenAnexos
{
    public const long TamanoMaximo = 25 * 1024 * 1024; // 25 MB

    public static async Task<(string NombreOriginal, string Ruta)> GuardarAsync(IBrowserFile archivo, string raizWeb, string carpeta)
    {
        if (archivo.Size > TamanoMaximo)
            throw new InvalidOperationException($"\"{archivo.Name}\" supera {TamanoMaximo / 1024 / 1024} MB.");
        var dir = Path.Combine(raizWeb, "uploads", carpeta);
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(archivo.Name);
        if (ext.Length > 10) ext = "";
        var nombre = $"{Guid.NewGuid():N}{ext}";
        await using (var origen = archivo.OpenReadStream(TamanoMaximo))
        await using (var destino = File.Create(Path.Combine(dir, nombre)))
            await origen.CopyToAsync(destino);
        return (archivo.Name, $"/uploads/{carpeta}/{nombre}");
    }

    public static bool EsImagen(string ruta) =>
        new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" }.Contains(Path.GetExtension(ruta).ToLowerInvariant());
}
