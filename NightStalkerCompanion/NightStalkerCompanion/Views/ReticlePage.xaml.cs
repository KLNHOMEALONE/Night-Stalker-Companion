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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = viewModel.AppearingAsync();
    }
}
