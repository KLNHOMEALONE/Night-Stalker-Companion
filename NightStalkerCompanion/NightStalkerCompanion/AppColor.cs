using Microsoft.Maui.Graphics;

namespace NightStalkerCompanion;

/// <summary>
/// Reads a colour out of the application resource dictionary by key.
///
/// Exists so that controls written in C# and the XAML style sheet resolve the
/// same token instead of keeping a second copy of the palette. Keys are the
/// design tokens from Resources/Styles/Colors.xaml, which mirror the accepted
/// mock-up (ui/prototype/index.html).
/// </summary>
public static class AppColor
{
    public static Color Get(string key)
    {
        var app = Application.Current;
        if (app?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return color;

        // Never paint nothing: a wrong visible colour is easier to notice than a
        // transparent control that silently disappeared.
        return Colors.Fuchsia;
    }
}
