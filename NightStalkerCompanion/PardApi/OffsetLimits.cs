namespace PardApi;

/// <summary>
/// What the scope lets each offset be — and the two axes do not agree.
///
/// <c>PosY</c> is strict: it holds to ±<see cref="YLimit"/>, and a value past that is not
/// something the screen should offer. <c>PosX</c> is not: the firmware has taken every X
/// it has been given, with no refusal and no visible ceiling, and no one has found the
/// number it stops at. The honest statement about X is that there is nothing to state, so
/// <see cref="WithinX"/> answers yes and the field writes whatever was typed.
///
/// That asymmetry is the reason this is a type and not a constant. A single "offset limit"
/// would either wall X at some invented number — refusing a value the sight accepts, which
/// this screen is specifically not allowed to do — or, reaching the other way, let Y wander
/// past a bound the device does enforce.
/// </summary>
public static class OffsetLimits
{
    /// <summary>
    /// How far Y goes in either direction. Known by the device, not by the app: values
    /// beyond it have never been accepted, and the same limit holds up and down.
    /// </summary>
    public const int YLimit = 75;

    /// <summary>
    /// Whether the scope takes this <c>PosY</c>. The boundary belongs to the range: ±75 is
    /// accepted, ±76 is not.
    /// </summary>
    public static bool WithinY(int value) => value >= -YLimit && value <= YLimit;

    /// <summary>
    /// Whether the scope takes this <c>PosX</c> — yes, whatever it is. Not a claim that
    /// every number is useful: one pushed far enough puts the mark off the visible field,
    /// and the read-back will then report a number that looks wrong on purpose. It is a
    /// claim only that the scope has never refused an X, so refusing one here would be the
    /// app's own invention.
    /// </summary>
    public static bool WithinX(int value) => true;

    /// <summary>
    /// The same question for whichever axis the field is editing. Callers ask this rather
    /// than the two named ones so that forgetting the axis fails here, in one place, and
    /// not in whichever branch was written first.
    /// </summary>
    public static bool Within(bool xAxis, int value) => xAxis ? WithinX(value) : WithinY(value);
}
