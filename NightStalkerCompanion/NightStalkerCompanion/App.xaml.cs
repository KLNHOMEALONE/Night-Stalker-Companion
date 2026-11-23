using Microsoft.Extensions.DependencyInjection;

namespace NightStalkerCompanion;

public partial class App : Application
{
	private readonly IServiceProvider serviceProvider;

	public App(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		this.serviceProvider = serviceProvider;

		// The design is dark-only and there is no light palette to fall back to:
		// every colour comes from the tokens in Resources/Styles/Colors.xaml, which
		// are copied from the accepted mock-up. Following the OS appearance would
		// mean a white scope app at dusk.
	}

	// The Shell comes from the container, like the reference Innowise.Music, and it is set
	// here rather than as a constructor parameter: AppShell paints itself with the tokens
	// from App.xaml, which only merge in the InitializeComponent above, so a Shell created
	// earlier would fail with "StaticResource not found for key Screen".
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var appShell = serviceProvider.GetRequiredService<AppShell>();
		return new Window(appShell);
	}
}
