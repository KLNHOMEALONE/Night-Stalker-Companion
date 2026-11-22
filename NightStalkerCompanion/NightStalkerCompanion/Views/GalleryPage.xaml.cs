using NightStalkerCompanion.ViewModels;

namespace NightStalkerCompanion.Views;

public partial class GalleryPage : ContentPage
{
    private readonly GalleryViewModel viewModel;

    public GalleryPage(GalleryViewModel viewModel)
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
