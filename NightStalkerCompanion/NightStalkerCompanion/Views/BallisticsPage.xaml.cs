using NightStalkerCompanion.Controls;
using NightStalkerCompanion.ViewModels;

namespace NightStalkerCompanion.Views;

public partial class BallisticsPage : ContentPage
{
    private readonly BallisticsViewModel viewModel;

    public BallisticsPage(BallisticsViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = viewModel.AppearingAsync();
    }

    /// <summary>
    /// Put the keystroke rule on a value field as it comes on screen.
    ///
    /// Added here rather than in the markup for the reason the mark screen states: declaring a
    /// behaviour as an <c>&lt;Entry.Behaviors&gt;</c> child makes the XAML source generator fail
    /// the whole page with MAUIG1001. It is attached per field because the seven fields are not
    /// the same type of number — one takes a minus, one is bounded by a mountain, three have a
    /// separator and three do not — and the rule reads the type off the row its field is bound
    /// to, which is the one thing that cannot be wired to the wrong field.
    /// </summary>
    private void OnFieldRealised(object? sender, EventArgs e)
    {
        if (sender is not Entry field
            || field.Behaviors.OfType<ProfileFieldEntryBehavior>().Any())
            return;                       // a field is realised once per row; do not rule it twice

        field.Behaviors.Add(new ProfileFieldEntryBehavior());
    }

    /// <summary>
    /// What a field means is the text standing in it, taken here rather than through the
    /// binding: a two-way Entry binding commits when focus goes away, and Enter does not take
    /// focus away, so on Enter the command would be handed the previous text — the same trap the
    /// offset fields on the mark screen fell into, and the same way out.
    ///
    /// Both keys of leaving a field come here: <c>Completed</c> for Enter, <c>Unfocused</c> for
    /// a finger that moved on to the next field or to the write button without pressing anything.
    /// Committing twice is harmless; a value that was typed and never committed is not.
    /// </summary>
    private void OnFieldValueCommitted(object? sender, EventArgs e)
    {
        if (sender is Entry { Text: { } text, BindingContext: FieldRow { Writable: true } row })
            viewModel.CommitCommand.Execute(row.CommitPrefix + text);
    }
}
