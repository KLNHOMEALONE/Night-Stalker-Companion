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
}
