namespace NightStalkerCompanion.Services;

/// <summary>
/// Reticle and colour captions. Kept apart so no screen invents its own wording
/// for the same number. The captions themselves are Russian on purpose — they
/// are what the shooter reads.
/// </summary>
public static class ReticleInfo
{
    /// <summary>
    /// Style 0 means there is no reticle at all — verified on a video frame from
    /// the scope (zero bright pixels in the centre of the frame). It is not
    /// "reticle number one", and in the UI it is a separate choice.
    /// </summary>
    public const int OffStyle = 0;

    public const int MaxStyle = 6;

    /// <summary>Preview asset name for a style. Style 0 has no preview by definition.</summary>
    public static string? PreviewAsset(int style)
        => style is > OffStyle and <= MaxStyle ? $"ret{style}.png" : null;

    /// <summary>
    /// Only two values have ever been seen on the device: 1 and 0. The captions
    /// come from the accepted mock-up (ui/prototype/index.html), where group A
    /// with Color=1 is called white and groups D/E with Color=0 red. The scope
    /// has shown no other colour, so an unknown value is printed as a number
    /// instead of a guess.
    /// </summary>
    public static string ColorLabel(int color) => color switch
    {
        1 => "белый",
        0 => "красный",
        _ => $"цвет {color}",
    };

    /// <summary>
    /// Human-readable reticle description, e.g. "стиль 3 · белый",
    /// "марка выключена", "стиль 4 · белый · см. −3/+5".
    /// </summary>
    public static string Describe(int style, int color, int posX, int posY)
    {
        if (style == OffStyle)
            return "марка выключена";

        var text = $"стиль {style} · {ColorLabel(color)}";
        if (posX != 0 || posY != 0)
            text += $" · см. {Signed(posX)}/{Signed(posY)}";
        return text;
    }

    private static string Signed(int v)
        => v > 0 ? $"+{v}" : v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
