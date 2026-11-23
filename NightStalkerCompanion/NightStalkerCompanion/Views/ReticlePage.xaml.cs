using NightStalkerCompanion.Controls;
using NightStalkerCompanion.ViewModels;

namespace NightStalkerCompanion.Views;

public partial class ReticlePage : ContentPage
{
    private readonly ReticleViewModel viewModel;

    public ReticlePage(ReticleViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();

        // Attached here rather than in the XAML: declaring a behaviour as an <Entry.Behaviors>
        // child makes the XAML source generator fail the whole page with MAUIG1001. Which axis a
        // field edits is the one thing the rule needs, so it is passed in by hand.
        XOffsetEntry.Behaviors.Add(new OffsetEntryBehavior { Axis = "X" });
        YOffsetEntry.Behaviors.Add(new OffsetEntryBehavior { Axis = "Y" });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = viewModel.AppearingAsync();
    }

    /// <summary>
    /// Enter in the offset field means "write". The text is taken from the field right here,
    /// not through the binding: a two-way Entry binding only commits on focus loss, Enter does
    /// not take focus away, and the command would get the old text. The field's axis arrives in
    /// StyleId; there are two of them, X and Y.
    /// </summary>
    private void OnOffsetCompleted(object? sender, EventArgs e)
    {
        if (sender is not Entry { StyleId: { Length: > 0 } axis } entry)
            return;

        if (axis == "X")
            viewModel.XInput = entry.Text;
        else
            viewModel.YInput = entry.Text;

        viewModel.ApplyOffsetCommand.Execute(axis);
    }
}
