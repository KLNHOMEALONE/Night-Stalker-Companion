namespace PardApi;

/// <summary>
/// Reading an offset the user typed by hand.
///
/// The screen used to offer steppers only, and a scope that is off by two hundred
/// clicks cannot be walked back that way, so a field was needed. What goes into it
/// is written straight into the device, so the rule is the narrow one: an optional
/// minus and digits, nothing else. There is no unit to type and no separator to
/// guess — the unit is the click the firmware counts, and the sign is the direction.
/// </summary>
public static class OffsetInput
{
    /// <summary>
    /// Whether <paramref name="text"/> is a whole offset, and what it means.
    /// Anything else — empty, a space, "1.5", "1 000", "12a", "+3", a typographic
    /// dash — is refused rather than rounded, trimmed or otherwise interpreted: a
    /// half-guessed number written to the scope aims the rifle at nowhere.
    /// </summary>
    public static bool TryParse(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
            return false;

        int digitsStart = text[0] == '-' ? 1 : 0;
        if (digitsStart == text.Length)
            return false;                                       // a lone minus

        for (int i = digitsStart; i < text.Length; i++)
            if (text[i] < '0' || text[i] > '9')
                return false;

        // The shape is already checked, so this only answers the question the shape
        // cannot: whether the number fits at all. 99999999999 is well-formed and no
        // offset.
        if (!int.TryParse(text[digitsStart..],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int magnitude))
            return false;

        value = text[0] == '-' ? -magnitude : magnitude;
        return true;
    }

    /// <summary>
    /// How a stored offset looks in the field. An ASCII minus, the same character the
    /// parser accepts, so what the screen prefills it can read back.
    /// </summary>
    public static string Format(int value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
