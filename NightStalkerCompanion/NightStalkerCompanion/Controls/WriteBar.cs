using NightStalkerCompanion.ViewModels;

namespace NightStalkerCompanion.Controls;

/// <summary>
/// The write status line — the thing the original app does not have at all.
/// Top line: what happened to the last write. Bottom line: what the client
/// actually saw when reading back. Anything a screen adds inside (buttons) lands
/// below, which is the mock-up's <c>.writebar</c>.
///
/// Written in C# rather than XAML because the only thing it varies on is state,
/// not layout.
/// </summary>
public class WriteBar : VerticalStackLayout
{
    private readonly BoxView dot;
    private readonly Label headline;
    private readonly Label explanation;

    public WriteBar()
    {
        Spacing = 4;
        Padding = new Thickness(16, 12, 16, 12);
        SetDynamicResource(BackgroundColorProperty, "Panel");

        Children.Add(new BoxView
        {
            HeightRequest = 1,
            HorizontalOptions = LayoutOptions.Fill,
            Color = AppColor.Get("Line")
        });

        dot = new BoxView
        {
            WidthRequest = 7,
            HeightRequest = 7,
            CornerRadius = 4,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Color = AppColor.Get("Warn")
        };

        headline = new Label
        {
            FontSize = 12,
            LineHeight = 1.4,
            VerticalOptions = LayoutOptions.Center
        };
        headline.SetDynamicResource(Label.TextColorProperty, "Ink");

        // A Grid rather than a StackLayout: the headline has to take the rest of the
        // width, and StackLayout's FillAndExpand is deprecated.
        var row = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
            }
        };
        row.Children.Add(dot);
        row.Children.Add(headline);

        explanation = new Label
        {
            FontSize = 11.5,
            LineHeight = 1.5,
            VerticalOptions = LayoutOptions.Start
        };
        explanation.SetDynamicResource(Label.TextColorProperty, "Ink3");

        Children.Add(row);
        Children.Add(explanation);

        Render(WriteBarView.Idle);
    }

    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State), typeof(AppState), typeof(WriteBar), propertyChanged: OnStateChanged);

    public AppState? State
    {
        get => (AppState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private static void OnStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var bar = (WriteBar)bindable;
        if (oldValue is AppState before)
            before.PropertyChanged -= bar.OnVerdictChanged;

        if (newValue is AppState after)
        {
            after.PropertyChanged += bar.OnVerdictChanged;
            bar.Render(WriteBarView.Of(after.Verdict));
        }
    }

    private void OnVerdictChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.Verdict) && sender is AppState state)
            Render(WriteBarView.Of(state.Verdict));
    }

    private void Render(WriteBarView view)
    {
        headline.Text = view.Headline;
        explanation.Text = view.Explanation;
        dot.Color = view.Dot;
    }
}
