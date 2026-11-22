using NightStalkerCompanion.ViewModels;

namespace NightStalkerCompanion.Views;

public partial class DevicePage : ContentPage
{
    private readonly DeviceViewModel viewModel;

    public DevicePage(DeviceViewModel viewModel)
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
