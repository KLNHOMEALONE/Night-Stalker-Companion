using NightStalkerCompanion.ViewModels;

namespace NightStalkerCompanion.Views;

public partial class UnitsPage : ContentPage
{
    private readonly UnitsViewModel viewModel;

    public UnitsPage(UnitsViewModel viewModel)
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
