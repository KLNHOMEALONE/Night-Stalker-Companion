namespace NightStalkerCompanion.Controls;

/// <summary>
/// A note about the state of the device — the thing the mock-up uses to say what
/// the scope cannot do. Left edge is a 2 px rule: grey for a plain finding,
/// amber for one that will bite the user.
///
/// MAUI borders cannot have a different stroke per side, so the rule is a child,
/// not the border.
/// </summary>
public class AlertCard : Border
{
    private readonly BoxView edge;
    private readonly Label text;

    public AlertCard()
    {
        edge = new BoxView
        {
            WidthRequest = 2,
            VerticalOptions = LayoutOptions.Fill,
            Color = AppColor.Get("Ink3")
        };
        edge.SetDynamicResource(BoxView.ColorProperty, "Ink3");

        text = new Label
        {
            FontSize = 12,
            LineHeight = 1.5,
            VerticalOptions = LayoutOptions.Center
        };
        text.SetDynamicResource(Label.TextColorProperty, "Ink2");

        var grid = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
            }
        };
        grid.Add(edge);
        grid.Add(text, 1);

        Content = grid;
        Padding = new Thickness(12, 10, 12, 10);
        Margin = new Thickness(16, 0, 16, 8);
        ApplyTokens();
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 };
    }

    private void ApplyTokens()
    {
        SetDynamicResource(BackgroundColorProperty, "Panel");
        SetDynamicResource(StrokeProperty, "Line");
        StrokeThickness = 1;
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(AlertCard), propertyChanged:
            (b, _, n) => ((AlertCard)b).text.Text = (string?)n);

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Amber edge for a warning, grey for a plain finding.</summary>
    public static readonly BindableProperty WarningProperty = BindableProperty.Create(
        nameof(Warning), typeof(bool), typeof(AlertCard), propertyChanged: (b, _, n) =>
        {
            var card = (AlertCard)b;
            card.edge.Color = AppColor.Get((bool)n ? "Warn" : "Ink3");
        });

    public bool Warning
    {
        get => (bool)GetValue(WarningProperty);
        set => SetValue(WarningProperty, value);
    }
}
