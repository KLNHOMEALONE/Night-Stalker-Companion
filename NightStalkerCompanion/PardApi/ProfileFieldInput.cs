using System.Globalization;

namespace PardApi;

/// <summary>
/// The two shapes a profile number comes in.
///
/// The firmware stores <c>Velocity</c>, <c>ZeroRange</c> and <c>Altitude</c> as whole
/// numbers and <c>BulletWt</c>, <c>BulletBC</c> and <c>ScopeHt</c> as fractions — it sends
/// <c>0.465</c> and <c>70.1</c> and answers the same back. A screen that does not know
/// which field is which either throws away the digits of a ballistic coefficient or
/// offers a separator to a field that has none.
/// </summary>
public enum ProfileFieldForm
{
    Whole,
    Decimal,
}

/// <summary>
/// What one profile field holds: a shape, whether it can go below zero, and where it stops.
///
/// Of the seven fields the ballistics screen edits, one is ever negative: temperature, which
/// is negative wherever the rifle is cold. A muzzle velocity, a bullet weight, a ballistic
/// coefficient, a height over bore, a zero range and a height above sea level are magnitudes,
/// and the numeric keyboard offers a minus to all seven the same way. Which field takes which
/// type is stated once, in the table of the screen that edits them, so that a field cannot
/// silently acquire a type of its own.
///
/// The bounds are in the units the field is shown in, which is why they belong to the type
/// rather than to the field: the same height above sea level is 8 000 of one field's metres
/// and 26 247 of another field's feet.
/// </summary>
public sealed record ProfileFieldType(
    ProfileFieldForm Form,
    bool NegativeAllowed = false,
    double? Minimum = null,
    double? Maximum = null)
{
    public static readonly ProfileFieldType Whole = new(ProfileFieldForm.Whole);
    public static readonly ProfileFieldType SignedWhole = new(ProfileFieldForm.Whole, NegativeAllowed: true);
    public static readonly ProfileFieldType Decimal = new(ProfileFieldForm.Decimal);

    /// <summary>Whether this field can show a minus at all — what the keyboard is told.</summary>
    public bool IsSigned => NegativeAllowed;

    /// <summary>The same type, held between two values in the units the field shows.</summary>
    public ProfileFieldType Between(double? minimum, double? maximum)
        => this with { Minimum = minimum, Maximum = maximum };

    /// <summary>
    /// Whether <paramref name="value"/> is a value this field can hold at all. The shape is
    /// checked by <see cref="ProfileFieldInput"/>; this is the part of the type that is about
    /// the world rather than about the text box.
    /// </summary>
    public bool Holds(double value)
        => (Minimum is null || value >= Minimum) && (Maximum is null || value <= Maximum);
}

/// <summary>
/// Where the profile's numbers stop, as far as anyone has established that.
///
/// There are two fields whose numbers end, and neither bound is a guess about the firmware:
/// <list type="bullet">
/// <item><description><see cref="AltitudeMaximum"/> is a fact about the Earth. The profile
/// holds a height above sea level, and the highest place a rifle is carried to is about that
/// high. A number past it is a slip of a finger on a keyboard, not a site.</description></item>
/// <item><description><see cref="TemperatureMinimum"/> is absolute zero, rounded up to the
/// whole degree the profile stores: below it there is no temperature, in the sense that there
/// is nothing there to measure. The top is not a law of nature — nothing the profile can be
/// pointed at is hot enough to matter, and a sight left in the sun is not either, so it is
/// stopped at <see cref="TemperatureMaximum"/>.</description></item>
/// </list>
/// The rest of the profile has no ceiling anyone has seen: a muzzle velocity has never been
/// refused for being large, and refusing one here would be the app inventing a limit the
/// device does not have — the same reason <see cref="OffsetLimits.WithinX"/> answers yes.
/// </summary>
public static class ProfileFieldLimits
{
    /// <summary>
    /// Lowest and highest height above sea level a profile can be pointed at, in metres: sea
    /// level, and about the top of the highest mountain there is.
    /// </summary>
    public const int AltitudeMinimum = 0;

    public const int AltitudeMaximum = 8000;

    /// <summary>Colddest temperature there is, and hottest one worth writing, in Celsius.</summary>
    public const int TemperatureMinimum = -272;

    public const int TemperatureMaximum = 100;
}

/// <summary>
/// Reading a profile value the user typed by hand, in the units the screen is showing.
///
/// The ballistics screen used to offer steppers only, and a step is a poor way in: setting
/// a muzzle velocity to 870 m/s is eight hundred taps, and a bullet weight is finer than
/// any stepper that screen had. So the fields became editable, and an editable field of a
/// device that will be written to needs a rule stated as exactly as this.
///
/// Three questions, deliberately separate, in the same shape as <see cref="OffsetInput"/>.
/// <see cref="IsDraft"/> answers what the field may contain while the number is being typed;
/// <see cref="TryParse"/> answers what is a value; <see cref="Wire"/> answers what goes to the
/// device. The text is always in the system on screen — yards, feet, grains, °F — because that
/// is what the user is reading, and the conversion back to the metric the firmware stores
/// happens once the number is complete.
/// </summary>
public static class ProfileFieldInput
{
    /// <summary>
    /// How many digits a field may hold. Not a claim about the device, which has never
    /// said where its numbers stop: past this the value is not a profile number in any
    /// unit system, and the field has no business showing it.
    /// </summary>
    public const int MaxDigits = 9;

    /// <summary>The longest text <see cref="MaxDigits"/> can appear in: sign, digits, point, decimals.</summary>
    public const int MaxLength = 1 + MaxDigits + 1 + MaxDigits;

    /// <summary>
    /// Whether the field may hold <paramref name="text"/> while the user is still typing it.
    ///
    /// This is the widest text that can still become a value of <paramref name="type"/>, and
    /// everything else never reaches the field at all: a letter, a space, a comma as
    /// separator, a second minus, a second separator, a separator in a whole field, a minus
    /// in a field that cannot be negative, digits past <see cref="MaxDigits"/>, a number
    /// past what the field can hold. A half-typed number — «-», «1», «1.» — is allowed,
    /// because that is how a number is typed.
    ///
    /// Refusing the rest on Enter would be too late: the wrong text would already be on
    /// screen looking like a profile value, and a keyboard is not a guarantee — the numeric
    /// keypad on some layouts has no separator and no minus, but a paste and a hardware
    /// keyboard both have everything.
    /// </summary>
    public static bool IsDraft(string? text, ProfileFieldType type)
    {
        if (!Shaped(text, type))
            return false;

        // A complete number the field cannot hold is refused while it is typed, not when it
        // is committed, so a field can never show an altitude above the mountain it is
        // standing on. A number that is not complete yet has no value to judge.
        return !Complete(text, out double value) || type.Holds(value);
    }

    /// <summary>
    /// Whether <paramref name="text"/> is a complete value of <paramref name="type"/>, and what
    /// it means in the units on screen. Anything still being typed — empty, a bare sign, a
    /// separator with nothing after it — is refused rather than finished by guessing: the
    /// number goes to the scope, and «1.» is either 1 or 1.0 or the start of 1.5, and none of
    /// those three is what the user typed.
    /// </summary>
    public static bool TryParse(string? text, ProfileFieldType type, out double value)
    {
        value = 0;
        return Shaped(text, type)
               && Complete(text, out value)
               && type.Holds(value);
    }

    /// <summary>
    /// The shape, and nothing else: digits, at most one separator in the right place, at most
    /// one minus in a field that takes one, no more digits than a field can hold. Empty text,
    /// a lone minus and a trailing separator all pass, because all three are on the way to a
    /// number rather than a number.
    /// </summary>
    private static bool Shaped(string? text, ProfileFieldType type)
    {
        if (string.IsNullOrEmpty(text))
            return true;                                              // typing starts from nothing

        int start = 0;
        if (text[0] == '-')
        {
            if (!type.NegativeAllowed)
                return false;                                         // a sign this field cannot hold
            if (text.Length == 1)
                return true;                                          // the sign arrives before its digits
            start = 1;
        }

        bool afterPoint = false;
        int digits = 0;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '.')
            {
                // A whole field has no separator, and a decimal field has exactly one —
                // and it never comes first: the device sends «0.465», never «.465».
                if (type.Form == ProfileFieldForm.Whole || afterPoint || digits == 0)
                    return false;
                afterPoint = true;
                continue;
            }

            if (c < '0' || c > '9')
                return false;                                         // letters, spaces, a comma, a pasted unit

            digits++;
            if (digits > MaxDigits)
                return false;
        }

        return digits > 0;                                            // a lone point is not a number either
    }

    /// <summary>Whether the shape is a whole number already, and what it is worth.</summary>
    private static bool Complete(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
            return false;

        string body = text[0] == '-' ? text[1..] : text;
        if (body.Length == 0 || body[^1] == '.')
            return false;                    // a sign with no digits, or a point with none: mid-typing

        // The shape is proven by Shaped, so this only answers what the shape cannot: whether the
        // digits make a number a double can hold.
        if (!double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value))
            return false;

        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// The text to send for a value the user typed, and the number of decimals it gets.
    ///
    /// Typing decides. A coefficient typed as <c>0.4658</c> leaves as four decimals and one typed
    /// as <c>0.47</c> leaves as two, because that is the exactness the user asked for and there is
    /// no sense in rounding a number the scope is about to be given. Where nothing was typed after
    /// the separator — a field stepped by −/+, or a whole one — the field's own tail decides, so a
    /// stepped field keeps sending the number of decimals the device sent it.
    ///
    /// The tail is formatting, and nothing more: <c>7.1</c> and <c>7.10</c> are one number, so
    /// neither choice above can change what the sight stores. <paramref name="value"/> is the
    /// number in whatever units the write is made in — the firmware stores metric, a screen may be
    /// showing yards — which is why the value comes in as a number and only the decimals come from
    /// the text and the field.
    /// </summary>
    public static int WireDecimals(string? typedText, int storedDecimals)
    {
        int typed = DecimalsOf(typedText);
        return typed > 0 ? typed : Math.Max(0, storedDecimals);
    }

    /// <summary><see cref="WireDecimals"/> applied: the number printed with those decimals.</summary>
    public static string Wire(double value, string? typedText, int storedDecimals)
        => value.ToString("F" + WireDecimals(typedText, storedDecimals), CultureInfo.InvariantCulture);

    /// <summary>How many digits <paramref name="text"/> carries after its separator; zero when it has none.</summary>
    public static int DecimalsOf(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        int point = text.IndexOf('.');
        return point < 0 ? 0 : text.Length - point - 1;
    }

    /// <summary>How many decimals the device's own text for a field carries.</summary>
    public static int StoredDecimals(string? stored) => DecimalsOf(stored);
}
