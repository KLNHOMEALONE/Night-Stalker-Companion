using Microsoft.Maui.Controls;

namespace NightStalkerCompanion.Controls;

/// <summary>
/// The instrument status bar — one per screen, always in the same place.
/// Four cells of "caption above, value below", split by a thin line. This is
/// <c>.status</c> in the mock-up; which four cells a screen shows is its own
/// business, so the captions come from XAML.
/// </summary>
public class StatusStrip : Border
{
    private readonly Label[] captions = new Label[CellCount];
    private readonly Label[] values = new Label[CellCount];

    private const int CellCount = 4;

    public StatusStrip()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            }
        };

        for (int i = 0; i < CellCount; i++)
        {
            var stack = new VerticalStackLayout { Padding = new Thickness(10, 8, 10, 8), Spacing = 2 };
            captions[i] = new Label();
            values[i] = new Label();
            captions[i].SetDynamicResource(StyleProperty, "StatusCaption");
            values[i].SetDynamicResource(StyleProperty, "StatusValue");
            stack.Children.Add(captions[i]);
            stack.Children.Add(values[i]);

            // A line on the left of every cell but the first, exactly like .status div.
            var cell = new Grid();
            cell.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            cell.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            if (i > 0)
                cell.Children.Add(new BoxView
                {
                    WidthRequest = 1,
                    Color = Line,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Fill
                });
            Grid.SetColumn(stack, i > 0 ? 1 : 0);
            cell.Children.Add(stack);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        Content = grid;
        SetDynamicResource(BackgroundColorProperty, "Panel");
        SetDynamicResource(StrokeProperty, "Line");
        StrokeThickness = 1;
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 };
        Margin = new Thickness(16, 0, 16, 12);
    }

    private static Microsoft.Maui.Graphics.Color Line
        => (Microsoft.Maui.Graphics.Color)Application.Current!.Resources["Line"];

    public static readonly BindableProperty Caption1Property = BindableProperty.Create(
        nameof(Caption1), typeof(string), typeof(StatusStrip), propertyChanged: (b, _, n) => ((StatusStrip)b).captions[0].Text = (string)n);
    public static readonly BindableProperty Caption2Property = BindableProperty.Create(
        nameof(Caption2), typeof(string), typeof(StatusStrip), propertyChanged: (b, _, n) => ((StatusStrip)b).captions[1].Text = (string)n);
    public static readonly BindableProperty Caption3Property = BindableProperty.Create(
        nameof(Caption3), typeof(string), typeof(StatusStrip), propertyChanged: (b, _, n) => ((StatusStrip)b).captions[2].Text = (string)n);
    public static readonly BindableProperty Caption4Property = BindableProperty.Create(
        nameof(Caption4), typeof(string), typeof(StatusStrip), propertyChanged: (b, _, n) => ((StatusStrip)b).captions[3].Text = (string)n);

    public static readonly BindableProperty Value1Property = BindableProperty.Create(
        nameof(Value1), typeof(string), typeof(StatusStrip), propertyChanged: (b, _, n) => ((StatusStrip)b).values[0].Text = (string)n);
    public static readonly BindableProperty Value2Property = BindableProperty.Create(
        nameof(Value2), typeof(string), typeof(StatusStrip), propertyChanged: (b, _, n) => ((StatusStrip)b).values[1].Text = (string)n);
    public static readonly BindableProperty Value3Property = BindableProperty.Create(
        nameof(Value3), typeof(string), typeof(StatusStrip), propertyChanged: (b, _, n) => ((StatusStrip)b).values[2].Text = (string)n);
    public static readonly BindableProperty Value4Property = BindableProperty.Create(
        nameof(Value4), typeof(string), typeof(StatusStrip), propertyChanged: (b, _, n) => ((StatusStrip)b).values[3].Text = (string)n);

    public string Caption1 { get => (string)GetValue(Caption1Property); set => SetValue(Caption1Property, value); }
    public string Caption2 { get => (string)GetValue(Caption2Property); set => SetValue(Caption2Property, value); }
    public string Caption3 { get => (string)GetValue(Caption3Property); set => SetValue(Caption3Property, value); }
    public string Caption4 { get => (string)GetValue(Caption4Property); set => SetValue(Caption4Property, value); }

    public string Value1 { get => (string)GetValue(Value1Property); set => SetValue(Value1Property, value); }
    public string Value2 { get => (string)GetValue(Value2Property); set => SetValue(Value2Property, value); }
    public string Value3 { get => (string)GetValue(Value3Property); set => SetValue(Value3Property, value); }
    public string Value4 { get => (string)GetValue(Value4Property); set => SetValue(Value4Property, value); }
}
