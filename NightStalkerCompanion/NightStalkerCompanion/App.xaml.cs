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

	// Shell приходит из контейнера, как в эталонном Innowise.Music, и именно здесь,
	// а не параметром конструктора: AppShell красится токенами из App.xaml, которые
	// сливаются только в InitializeComponent выше, — созданный раньше Shell упал бы с
	// «StaticResource not found for key Screen».
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var appShell = serviceProvider.GetRequiredService<AppShell>();
		return new Window(appShell);
	}
}
