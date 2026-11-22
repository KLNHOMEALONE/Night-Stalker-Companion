namespace NightStalkerCompanion;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// The design is dark-only and there is no light palette to fall back to:
		// every colour comes from the tokens in Resources/Styles/Colors.xaml, which
		// are copied from the accepted mock-up. Following the OS appearance would
		// mean a white scope app at dusk.
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
