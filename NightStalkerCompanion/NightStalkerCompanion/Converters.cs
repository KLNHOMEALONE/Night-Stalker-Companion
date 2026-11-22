using System.Globalization;

namespace NightStalkerCompanion;

/// <summary>
/// Shows a block only when the bound value carries something. Used for previews
/// that may legitimately not exist — style 0 has no reticle image at all, and an
/// empty image box would read as a broken asset.
/// </summary>
public class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            null => false,
            string s => s.Length > 0,
            _ => true,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverse of <see cref="NotNullToBoolConverter" />: shows the placeholder that
/// stands where a preview would otherwise be a blank box.</summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            null => true,
            string s => s.Length == 0,
            _ => false,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Plain negation for "show the honest empty state instead".</summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>
/// true → one design token, false → another. Used by the selectable chips and
/// tiles: a selected group is outlined in the accent colour, an unselected one in
/// the plain line colour, and only the border changes — the mock-up never fills a
/// selection in with colour.
///
/// ConverterParameter: "onKey,offKey", for example "Accent,Line".
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string[] keys = ((string?)parameter ?? "Accent,Line").Split(',');
        bool on = value is true;
        return AppColor.Get(on ? keys[0] : keys.Length > 1 ? keys[1] : "Line");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
