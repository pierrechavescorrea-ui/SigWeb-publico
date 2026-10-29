namespace SigWeb.Data.Trazado;

// Ayuda a emparejar el texto de una torre tal como llega de un Excel de
// otro módulo (Ensayos, y en el futuro cualquiera que cargue tablas)
// contra los códigos reales de ElementoTrazado, cuando no coinciden
// exactamente por inconsistencias de digitación (ej. el informe de
// laboratorio dice "102NN" pero en Trazado la torre está como "102NNN").
// No decide nada solo: calcula candidatos parecidos para que el usuario
// confirme, y esa confirmación se guarda aparte (ver AliasTorre) para
// que la próxima carga con el mismo texto ya no tenga que preguntar.
public static class ResolverTorre
{
    // Distancia de Levenshtein clásica (cuántas ediciones de un caracter
    // -insertar, borrar, cambiar- hacen falta para pasar de "a" a "b").
    public static int Distancia(string a, string b)
    {
        a ??= "";
        b ??= "";
        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + costo);
            }
        }
        return dp[a.Length, b.Length];
    }

    // Códigos existentes más parecidos a "texto", del más al menos
    // parecido, descartando los que ya ni se acercan (la tolerancia
    // crece un poco con el largo del código, para no ser demasiado
    // estricta con códigos largos ni demasiado laxa con los cortos).
    public static List<string> SugerirCodigosParecidos(string? texto, IEnumerable<string> codigosExistentes, int maximo = 4)
    {
        string t = (texto ?? "").Trim().ToUpperInvariant();
        if (t.Length == 0) return new();

        return codigosExistentes
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Select(c => new { Codigo = c, Dist = Distancia(t, c.ToUpperInvariant()) })
            .Where(x => x.Dist > 0 && x.Dist <= Math.Max(2, (t.Length + x.Codigo.Length) / 6))
            .OrderBy(x => x.Dist)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .Take(maximo)
            .Select(x => x.Codigo)
            .ToList();
    }
}
