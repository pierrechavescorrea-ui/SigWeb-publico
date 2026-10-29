namespace SigWeb.Data.Trazado;

// Compara dos códigos de torre para detectar el caso "es la misma torre,
// pero le cambiaron el nombre" (ej. "99" pasó a llamarse "99N"). Se usa
// tanto al importar KMZ/Excel de coordenadas como al importar la tabla
// de torres, para que el mismo criterio decida en los dos casos.
public static class DetectorNombresParecidos
{
    public static bool SonParecidos(string a, string b)
    {
        a = a.Trim().ToUpperInvariant();
        b = b.Trim().ToUpperInvariant();
        if (a == b) return false;
        if (a.Length == 0 || b.Length == 0) return false;

        // Uno es "prefijo" del otro: "102" vs "102N", "102" vs "102NN".
        if (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)) return true;

        // Nombres de longitud parecida y muy pocos caracteres de diferencia.
        if (Math.Abs(a.Length - b.Length) <= 2 && DistanciaEdicion(a, b) <= 2) return true;

        return false;
    }

    // Distancia de Levenshtein clásica (número mínimo de inserciones,
    // borrados o cambios de un carácter para pasar de a a b).
    public static int DistanciaEdicion(string a, string b)
    {
        var costos = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) costos[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            int anterior = costos[0];
            costos[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int actual = costos[j];
                costos[j] = a[i - 1] == b[j - 1]
                    ? anterior
                    : 1 + Math.Min(anterior, Math.Min(costos[j], costos[j - 1]));
                anterior = actual;
            }
        }
        return costos[b.Length];
    }
}
