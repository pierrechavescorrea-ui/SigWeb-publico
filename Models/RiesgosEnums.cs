namespace SigWeb.Models;

// Escalas de la matriz de riesgos tal como vienen del Excel de la empresa
// (listas de validación "Probabilidad" e "Impacto", 5 niveles cada una).
public enum ProbabilidadRiesgo
{
    MuyBaja = 1,
    Baja = 2,
    Moderada = 3,
    Alta = 4,
    MuyAlta = 5
}

public enum ImpactoRiesgo
{
    Inferior = 1,
    Menor = 2,
    Importante = 3,
    Mayor = 4,
    Superior = 5
}

// Resultado de cruzar probabilidad × impacto en la matriz 5×5.
// Nombres tal como los define la hoja "Valoración" del libro de la matriz
// (MATRIZ_RIESGOS_LT_500_kV_SNT, filas 19–25): Bajo, Moderado, Alto, Extremo.
public enum NivelRiesgo
{
    Bajo = 0,
    Moderado = 1,
    Alto = 2,
    Extremo = 3
}

// La columna del Excel se llama "Riesgo / Oportunidad" y su texto empieza
// por "RIESGO:" u "OPORTUNIDAD:"; aquí es un campo propio.
public enum TipoRiesgo
{
    Riesgo = 0,
    Oportunidad = 1
}

// Ciclo de vida de una revisión de la matriz. Solo una versión puede estar
// Vigente: es sobre la que se hacen los seguimientos nuevos.
public enum EstadoVersionMatriz
{
    Borrador = 0,
    Vigente = 1,
    Historica = 2
}

// Qué se concluyó en un seguimiento periódico del riesgo.
public enum ResultadoSeguimiento
{
    NoMaterializado = 0,
    EnObservacion = 1,
    Materializado = 2
}

// Listas de un riesgo que antes venían numeradas en una sola celda.
public enum TipoElementoRiesgo
{
    Causa = 0,
    Consecuencia = 1,
    Disparador = 2,
    PlanRespuesta = 3,
    Seguimiento = 4
}

// Estado de un hallazgo (la materialización concreta en una torre,
// pata o localización).
public enum EstadoHallazgo
{
    Abierto = 0,
    EnTratamiento = 1,
    Cerrado = 2
}

// Cruce probabilidad × impacto, copiado de la hoja "Valoración" del libro
// de la matriz (MATRIZ_RIESGOS_LT_500_kV_SNT V5, filas 19–25), que es la
// instrucción de diligenciamiento oficial. Los colores son los mismos que
// usa el bloque 5×5 dibujado en cada riesgo (verde 00B050 = Bajo,
// amarillo FFFF00 = Moderado, naranja FFC000 = Alto, rojo FF0000 = Extremo).
// Los pesos numéricos también salen de esa hoja (fila 20 para impacto,
// columna C para probabilidad). Si la empresa cambia la escala, se cambia aquí.
public static class EscalaRiesgo
{
    // Índice: [probabilidad 1..5 (MuyBaja→MuyAlta), impacto 1..5 (Inferior→Superior)]
    private static readonly NivelRiesgo[,] Mapa =
    {
        // Inferior           Menor                Importante           Mayor                Superior
        { NivelRiesgo.Bajo,     NivelRiesgo.Bajo,     NivelRiesgo.Bajo,     NivelRiesgo.Bajo,     NivelRiesgo.Moderado }, // Muy baja
        { NivelRiesgo.Bajo,     NivelRiesgo.Bajo,     NivelRiesgo.Moderado, NivelRiesgo.Moderado, NivelRiesgo.Moderado }, // Baja
        { NivelRiesgo.Bajo,     NivelRiesgo.Moderado, NivelRiesgo.Moderado, NivelRiesgo.Alto,     NivelRiesgo.Alto },     // Moderada
        { NivelRiesgo.Moderado, NivelRiesgo.Moderado, NivelRiesgo.Alto,     NivelRiesgo.Alto,     NivelRiesgo.Extremo },  // Alta
        { NivelRiesgo.Moderado, NivelRiesgo.Moderado, NivelRiesgo.Alto,     NivelRiesgo.Extremo,  NivelRiesgo.Extremo }   // Muy alta
    };

    // Pesos de la hoja "Valoración": probabilidad 0,1–0,9 e impacto 0,05–0,8.
    // Su producto es la puntuación con la que se priorizan los riesgos.
    public static double Peso(ProbabilidadRiesgo p) => p switch
    {
        ProbabilidadRiesgo.MuyAlta => 0.9,
        ProbabilidadRiesgo.Alta => 0.7,
        ProbabilidadRiesgo.Moderada => 0.5,
        ProbabilidadRiesgo.Baja => 0.3,
        _ => 0.1
    };

    public static double Peso(ImpactoRiesgo i) => i switch
    {
        ImpactoRiesgo.Superior => 0.8,
        ImpactoRiesgo.Mayor => 0.4,
        ImpactoRiesgo.Importante => 0.2,
        ImpactoRiesgo.Menor => 0.1,
        _ => 0.05
    };

    public static double Puntuacion(ProbabilidadRiesgo p, ImpactoRiesgo i) => Math.Round(Peso(p) * Peso(i), 4);

    public static NivelRiesgo Nivel(ProbabilidadRiesgo p, ImpactoRiesgo i) => Mapa[(int)p - 1, (int)i - 1];

    public static string Nombre(NivelRiesgo n) => n switch
    {
        NivelRiesgo.Bajo => "Bajo",
        NivelRiesgo.Moderado => "Moderado",
        NivelRiesgo.Alto => "Alto",
        _ => "Extremo"
    };

    // Mismos colores del Excel.
    public static string Color(NivelRiesgo n) => n switch
    {
        NivelRiesgo.Bajo => "#00B050",
        NivelRiesgo.Moderado => "#FFFF00",
        NivelRiesgo.Alto => "#FFC000",
        _ => "#FF0000"
    };

    // Texto legible sobre ese color: solo el rojo admite texto blanco;
    // sobre verde, amarillo y naranja el blanco no contrasta.
    public static string ColorTexto(NivelRiesgo n) => n == NivelRiesgo.Extremo ? "#ffffff" : "#1f2a3a";

    public static string Nombre(ProbabilidadRiesgo p) => p switch
    {
        ProbabilidadRiesgo.MuyBaja => "Muy baja",
        ProbabilidadRiesgo.Baja => "Baja",
        ProbabilidadRiesgo.Moderada => "Moderada",
        ProbabilidadRiesgo.Alta => "Alta",
        _ => "Muy alta"
    };

    public static string Sigla(ProbabilidadRiesgo p) => p switch
    {
        ProbabilidadRiesgo.MuyBaja => "MB",
        ProbabilidadRiesgo.Baja => "B",
        ProbabilidadRiesgo.Moderada => "M",
        ProbabilidadRiesgo.Alta => "A",
        _ => "MA"
    };

    public static string Nombre(ImpactoRiesgo i) => i switch
    {
        ImpactoRiesgo.Inferior => "Inferior",
        ImpactoRiesgo.Menor => "Menor",
        ImpactoRiesgo.Importante => "Importante",
        ImpactoRiesgo.Mayor => "Mayor",
        _ => "Superior"
    };

    public static string Sigla(ImpactoRiesgo i) => i switch
    {
        ImpactoRiesgo.Inferior => "I",
        ImpactoRiesgo.Menor => "M",
        ImpactoRiesgo.Importante => "IM",
        ImpactoRiesgo.Mayor => "MA",
        _ => "S"
    };

    public static readonly ProbabilidadRiesgo[] Probabilidades =
    {
        ProbabilidadRiesgo.MuyAlta, ProbabilidadRiesgo.Alta, ProbabilidadRiesgo.Moderada,
        ProbabilidadRiesgo.Baja, ProbabilidadRiesgo.MuyBaja
    };

    public static readonly ImpactoRiesgo[] Impactos =
    {
        ImpactoRiesgo.Inferior, ImpactoRiesgo.Menor, ImpactoRiesgo.Importante,
        ImpactoRiesgo.Mayor, ImpactoRiesgo.Superior
    };

    // Texto del Excel → enum. Devuelve null si no coincide con ninguno.
    public static ProbabilidadRiesgo? LeerProbabilidad(string? t) => Norm(t) switch
    {
        "muyalta" => ProbabilidadRiesgo.MuyAlta,
        "alta" => ProbabilidadRiesgo.Alta,
        "moderada" or "media" => ProbabilidadRiesgo.Moderada,
        "baja" => ProbabilidadRiesgo.Baja,
        "muybaja" => ProbabilidadRiesgo.MuyBaja,
        _ => null
    };

    public static ImpactoRiesgo? LeerImpacto(string? t) => Norm(t) switch
    {
        "superior" => ImpactoRiesgo.Superior,
        "mayor" => ImpactoRiesgo.Mayor,
        "importante" => ImpactoRiesgo.Importante,
        "menor" => ImpactoRiesgo.Menor,
        "inferior" => ImpactoRiesgo.Inferior,
        _ => null
    };

    private static string Norm(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "";
        var s = t.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var c in s.Normalize(System.Text.NormalizationForm.FormD))
            if (char.IsLetterOrDigit(c) && System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }
}
