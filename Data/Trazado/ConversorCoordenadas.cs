namespace SigWeb.Data.Trazado;

public enum SistemaCoordenadas
{
    Geograficas,     // Ya vienen en grados decimales (lon, lat)
    MagnaBogota,     // MAGNA-SIRGAS / Origen Bogotá (EPSG:3116) — "coordenadas planas"
    Utm17N,
    Utm18N,
    Utm19N
}

// Convierte coordenadas X/Y de los sistemas planos que se usan en
// Colombia (MAGNA-SIRGAS Origen Bogotá y UTM 17N/18N/19N) a latitud y
// longitud (grados decimales, WGS84) para poder ubicarlas en el mapa.
// También intenta adivinar cuál de esos sistemas trae el archivo, a
// partir del rango típico de magnitud de X y Y, para preseleccionar
// algo razonable — pero SIEMPRE deja que el usuario confirme o cambie
// el sistema antes de importar, porque de esto depende la precisión.
public static class ConversorCoordenadas
{
    // Parámetros de cada sistema: elipsoide, falso este/norte, meridiano
    // central, latitud de origen y factor de escala.
    private record Parametros(
        double SemiEjeMayor, double Achatamiento,
        double FalsoEste, double FalsoNorte,
        double MeridianoCentralGrados, double LatitudOrigenGrados,
        double FactorEscala);

    private static Parametros ParametrosDe(SistemaCoordenadas sistema) => sistema switch
    {
        // GRS80 (prácticamente idéntico a WGS84 para este propósito).
        SistemaCoordenadas.MagnaBogota => new Parametros(
            SemiEjeMayor: 6378137.0, Achatamiento: 1.0 / 298.257222101,
            FalsoEste: 1_000_000.0, FalsoNorte: 1_000_000.0,
            MeridianoCentralGrados: -74.077508, LatitudOrigenGrados: 4.596200,
            FactorEscala: 1.0),

        SistemaCoordenadas.Utm17N => ParametrosUtm(-81.0),
        SistemaCoordenadas.Utm18N => ParametrosUtm(-75.0),
        SistemaCoordenadas.Utm19N => ParametrosUtm(-69.0),

        _ => throw new ArgumentException("Este sistema no requiere conversión.", nameof(sistema))
    };

    private static Parametros ParametrosUtm(double meridianoCentral) => new(
        SemiEjeMayor: 6378137.0, Achatamiento: 1.0 / 298.257223563, // WGS84
        FalsoEste: 500_000.0, FalsoNorte: 0.0, // hemisferio norte (todo Colombia salvo un pedazo mínimo del Amazonas)
        MeridianoCentralGrados: meridianoCentral, LatitudOrigenGrados: 0.0,
        FactorEscala: 0.9996);

    // Rango aproximado de Colombia continental, para las heurísticas.
    private const double LonMin = -82.0, LonMax = -66.0;
    private const double LatMin = -4.5, LatMax = 13.5;

    // Adivina el sistema más probable a partir de la magnitud de X/Y.
    // Es solo un punto de partida: el usuario lo confirma o lo cambia
    // antes de importar.
    public static SistemaCoordenadas Detectar(double x, double y)
    {
        if (x >= LonMin && x <= LonMax && y >= LatMin && y <= LatMax)
        {
            return SistemaCoordenadas.Geograficas;
        }

        // Coordenadas planas con origen Bogotá: X y Y suelen estar cerca
        // de 1'000.000 (el falso este/norte), normalmente entre 700.000 y 1'300.000.
        if (x is >= 700_000 and <= 1_300_000 && y is >= 700_000 and <= 1_300_000)
        {
            return SistemaCoordenadas.MagnaBogota;
        }

        // UTM: Este típico 100.000–900.000, Norte 0–10'000.000. No se
        // puede saber la zona (17N/18N/19N) solo con la magnitud, así
        // que se asume 18N (la más común en el centro del país) — el
        // usuario debe confirmarla o cambiarla.
        if (x is >= 100_000 and <= 900_000 && y is >= 0 and <= 10_000_000)
        {
            return SistemaCoordenadas.Utm18N;
        }

        // Nada calzó: se deja Geográficas por defecto, pero lo normal es
        // que esto dispare una advertencia en el llamador.
        return SistemaCoordenadas.Geograficas;
    }

    public static (double Latitud, double Longitud) Convertir(double x, double y, SistemaCoordenadas sistema)
    {
        if (sistema == SistemaCoordenadas.Geograficas)
        {
            return (Latitud: y, Longitud: x);
        }

        var p = ParametrosDe(sistema);
        return InversaTransversaMercator(x, y, p);
    }

    // Fórmulas estándar de Snyder para la Transversa de Mercator inversa
    // (las mismas que usan la mayoría de calculadoras UTM/Gauss-Krüger).
    // Precisión de unos pocos centímetros dentro del rango normal de uso
    // de cada zona — más que suficiente para ubicar una torre en un mapa.
    private static (double Latitud, double Longitud) InversaTransversaMercator(double este, double norte, Parametros p)
    {
        double a = p.SemiEjeMayor;
        double f = p.Achatamiento;
        double e2 = f * (2 - f);
        double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));
        double k0 = p.FactorEscala;

        double phi0 = ARadianes(p.LatitudOrigenGrados);
        double lambda0 = ARadianes(p.MeridianoCentralGrados);

        double M0 = a * (
            (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256) * phi0
            - (3 * e2 / 8 + 3 * e2 * e2 / 32 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(2 * phi0)
            + (15 * e2 * e2 / 256 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(4 * phi0)
            - (35 * e2 * e2 * e2 / 3072) * Math.Sin(6 * phi0));

        double M = M0 + (norte - p.FalsoNorte) / k0;
        double mu = M / (a * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));

        double phi1 = mu
            + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu)
            + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu)
            + (151 * Math.Pow(e1, 3) / 96) * Math.Sin(6 * mu)
            + (1097 * Math.Pow(e1, 4) / 512) * Math.Sin(8 * mu);

        double ePrima2 = e2 / (1 - e2);
        double C1 = ePrima2 * Math.Pow(Math.Cos(phi1), 2);
        double T1 = Math.Pow(Math.Tan(phi1), 2);
        double N1 = a / Math.Sqrt(1 - e2 * Math.Pow(Math.Sin(phi1), 2));
        double R1 = a * (1 - e2) / Math.Pow(1 - e2 * Math.Pow(Math.Sin(phi1), 2), 1.5);
        double D = (este - p.FalsoEste) / (N1 * k0);

        double phi = phi1 - (N1 * Math.Tan(phi1) / R1) * (
            D * D / 2
            - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * ePrima2) * Math.Pow(D, 4) / 24
            + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * ePrima2 - 3 * C1 * C1) * Math.Pow(D, 6) / 720);

        double lambda = lambda0 + (
            D
            - (1 + 2 * T1 + C1) * Math.Pow(D, 3) / 6
            + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * ePrima2 + 24 * T1 * T1) * Math.Pow(D, 5) / 120
        ) / Math.Cos(phi1);

        return (Latitud: AGrados(phi), Longitud: AGrados(lambda));
    }

    private static double AGrados(double radianes) => radianes * 180.0 / Math.PI;
    private static double ARadianes(double grados) => grados * Math.PI / 180.0;
}