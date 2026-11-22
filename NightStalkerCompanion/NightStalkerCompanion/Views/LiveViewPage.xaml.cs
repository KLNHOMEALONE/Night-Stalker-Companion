using NightStalkerCompanion.ViewModels;

namespace NightStalkerCompanion.Views;

public partial class LiveViewPage : ContentPage
{
    private readonly LiveViewModel viewModel;

    public LiveViewPage(LiveViewModel viewModel)
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
