namespace NightStalkerCompanion.Services;

/// <summary>Drop calculation input — everything in metric, as the device stores it.</summary>
public sealed record BallisticsInput
{
    public required double VelocityMps { get; init; }
    public required double ScopeHeightMm { get; init; }

    /// <summary>Zero range in metres. Converted already if the device is in yards.</summary>
    public required double ZeroRangeMeters { get; init; }

    /// <summary>Shown for honesty; this model does not use the mass.</summary>
    public double BulletGrams { get; init; }

    public double BulletBC { get; init; }

    public static BallisticsInput? From(PardApi.BallisticProfile? profile)
    {
        if (profile is null) return null;
        double v = profile.Number("Velocity").Value;
        double h = profile.Number("ScopeHt").Value;
        double z = profile.Number("ZeroRange").Value;
        if (v <= 0 || z <= 0) return null;

        // The profile's own units are the device's: ZeroRange=41 with
        // DistanceUnit=1 means 41 yards, not 41 metres (notes/APP_FIELDS.md).
        bool imperial = (int)profile.Number("DistanceUnit").Value != 0;
        if (imperial) z *= 0.9144;

        return new BallisticsInput
        {
            VelocityMps = v,
            ScopeHeightMm = h,
            ZeroRangeMeters = z,
            BulletGrams = profile.Number("BulletWt").Value,
            BulletBC = profile.Number("BulletBC").Value,
        };
    }
}

/// <summary>
/// Drop table for the Ballistics screen.
///
/// THE MODEL. Vacuum ballistics plus the zeroing angle:
///
///     y(x) = −h + x·tanθ − g·x² / (2·v₀²),
///     tanθ = (h + g·z² / (2·v₀²)) / z,
///
/// where h is the sight height over the bore, z the zero range, v₀ the muzzle
/// velocity. Closed form, no constants fitted to make a picture match.
///
/// WHAT IS MISSING. Air drag. Bullet mass and BC do not enter, because the
/// profile alone does not contain what turns a BC into a force: there is no
/// known calibre or diameter, so there is no sectional density. With drag the
/// drop at 250 m is roughly twice as large — this table under-reports, and does
/// so worse the farther out you go.
///
/// THEREFORE the table is labelled a client-side calculation, not the device's
/// word (open question 1 in notes/HANDOFF.md). The right source is the scope's
/// own correction table: the stock app reads it as table_data / mem_table with
/// mem_id=%d&amp;group_id=%d and gets a value plus a unit per distance
/// (notes/APP_FIELDS.md §4). Once that command is captured, the table becomes the
/// device's, not ours.
/// </summary>
public static class BallisticsCalculator
{
    private const double Gravity = 9.80665;

    /// <summary>Model caption — the screen prints this verbatim under the table.</summary>
    public const string ModelName = "расчёт клиента: вакуум + угол пристрелки, без сопротивления воздуха";

    /// <summary>Whether the table has been checked against the scope's own screen. It has not.</summary>
    public const bool VerifiedAgainstScope = false;

    /// <summary>Row distances, in the unit system currently displayed.</summary>
    public static readonly int[] DisplayDistances = [50, 100, 150, 200, 250];

    public static IReadOnlyList<DropRow> Table(BallisticsInput? input, UnitSystem units)
    {
        if (input is null) return Array.Empty<DropRow>();

        var rows = new List<DropRow>(DisplayDistances.Length);
        foreach (int shown in DisplayDistances)
        {
            // In imperial mode "100" is 100 yards, i.e. 91.44 m of flight.
            double meters = units == UnitSystem.Metric ? shown : shown * 0.9144;
            double dropMeters = DropMeters(input, meters);
            double ms = meters / input.VelocityMps * 1000.0;

            // Drop is shown in centimetres in both systems: yards per hundred
            // gives a fraction, and "it drops this much" is read in centimetres.
            rows.Add(new DropRow(
                $"{shown} {Units.DistanceUnit(units)}",
                $"{dropMeters * 100:0} см",
                $"{ms:0} мс",
                IsZero: Math.Abs(dropMeters) < 0.005));
        }

        return rows;
    }

    /// <summary>
    /// Bullet position relative to the line of sight, metres. Negative is below.
    /// </summary>
    public static double DropMeters(BallisticsInput input, double xMeters)
    {
        double h = input.ScopeHeightMm / 1000.0;
        double z = input.ZeroRangeMeters;
        double v = input.VelocityMps;

        double fall(double x) => Gravity * x * x / (2 * v * v);
        double tanTheta = (h + fall(z)) / z;

        return -h + xMeters * tanTheta - fall(xMeters);
    }
}
