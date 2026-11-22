namespace NightStalkerCompanion.Services;

/// <summary>Unit system. The device stores metric; yards are a display mode.</summary>
public enum UnitSystem
{
    Metric,
    Imperial,
}

/// <summary>
/// Conversion between the two systems. Plain arithmetic, no "device flavour":
/// the firmware holds <c>Velocity=285</c> while the stock app displayed 935 ft/s,
/// so the stored number is metric and yards are display only.
///
/// The ballistic coefficient is never converted: the device keeps the same number
/// in both modes, and changing it would silently swap the bullet. Formally BC
/// belongs to a drag model, so "dimensionless" here is the device's convention,
/// not physics — say so in the UI rather than explain it away.
///
/// Length, speed and distance factors are exact by definition under the 1959
/// international yard and pound agreement (0.9144 m, 0.45359237 kg). GrPerG is the
/// odd one out: 1 g is 7000/453.59237 = 15.4323583529... gr, so the literal below is
/// off by about 9e-9 relative. Invisible at the 0.01 g input step; it would only
/// matter if a shown value ever had to round trip back bit for bit.
/// </summary>
public static class Units
{
    private const double FtPerM = 1 / 0.3048;
    private const double YdPerM = 1 / 0.9144;
    private const double InPerMm = 1 / 25.4;
    private const double GrPerG = 15.432358492;

    public static double SpeedToShown(double metersPerSecond, UnitSystem u)
        => u == UnitSystem.Metric ? metersPerSecond : metersPerSecond * FtPerM;

    public static double MassToShown(double grams, UnitSystem u)
        => u == UnitSystem.Metric ? grams : grams * GrPerG;

    public static double LengthToShown(double millimeters, UnitSystem u)
        => u == UnitSystem.Metric ? millimeters : millimeters * InPerMm;

    public static double DistanceToShown(double meters, UnitSystem u)
        => u == UnitSystem.Metric ? meters : meters * YdPerM;

    public static double AltitudeToShown(double meters, UnitSystem u)
        => u == UnitSystem.Metric ? meters : meters * FtPerM;

    public static double TemperatureToShown(double celsius, UnitSystem u)
        => u == UnitSystem.Metric ? celsius : celsius * 9 / 5 + 32;

    public static string SpeedUnit(UnitSystem u) => u == UnitSystem.Metric ? "м/с" : "ft/s";
    public static string MassUnit(UnitSystem u) => u == UnitSystem.Metric ? "г" : "gr";
    public static string LengthUnit(UnitSystem u) => u == UnitSystem.Metric ? "мм" : "in";
    public static string DistanceUnit(UnitSystem u) => u == UnitSystem.Metric ? "м" : "yd";
    public static string AltitudeUnit(UnitSystem u) => u == UnitSystem.Metric ? "м" : "ft";
    public static string TemperatureUnit(UnitSystem u) => u == UnitSystem.Metric ? "°C" : "°F";

    /// <summary>
    /// The input step depends on the system: 0.01 g is 0.15 gr, so in imperial
    /// the mass steps by 0.1 gr rather than by "the same number after conversion,
    /// with a lot of digits".
    /// </summary>
    public static double MassStep(UnitSystem u) => u == UnitSystem.Metric ? 0.01 : 0.1;

    public static double SpeedStep(UnitSystem u) => 1;
    public static double DistanceStep(UnitSystem u) => 1;
    public static double LengthStep(UnitSystem u) => u == UnitSystem.Metric ? 0.1 : 0.01;
    public static double BcStep() => 0.001;

    /// <summary>
    /// A shown value printed with exactly as many decimals as the input step
    /// carries meaning.
    /// </summary>
    public static string Format(double shown, double step)
    {
        int decimals = step >= 1 ? 0 : step >= 0.1 ? 1 : step >= 0.01 ? 2 : 3;
        return shown.ToString($"F{decimals}", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>From a shown number back to what we send the device: always metric.</summary>
    public static double ShownToStored(double shown, UnitSystem u, Quantity what) => u switch
    {
        UnitSystem.Metric => shown,
        _ => what switch
        {
            Quantity.Speed => shown / FtPerM,
            Quantity.Mass => shown / GrPerG,
            Quantity.Length => shown / InPerMm,
            Quantity.Distance => shown / YdPerM,
            Quantity.Altitude => shown / FtPerM,
            Quantity.Temperature => (shown - 32) * 5 / 9,
            Quantity.Bc => shown,
            // A new Quantity must be added on purpose; a compiler warning is the price.
            _ => shown,
        }
    };

    public enum Quantity
    {
        Speed,
        Mass,
        Length,
        Distance,
        Altitude,
        Temperature,
        Bc,
    }
}
