using NightStalkerCompanion.Views;

namespace NightStalkerCompanion;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Screens that are pushed rather than tabbed. Live view is a mode with a
        // "back" line instead of a navigation bar — going into it and out of it is
        // the point, so it is not a tab (ui/prototype/index.html, screen 01).
        Routing.RegisterRoute("live", typeof(LiveViewPage));
        Routing.RegisterRoute("reticle", typeof(ReticlePage));
        Routing.RegisterRoute("ballistics", typeof(BallisticsPage));
        Routing.RegisterRoute("units", typeof(UnitsPage));
    }
}
