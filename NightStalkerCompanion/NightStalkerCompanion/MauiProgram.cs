using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NightStalkerCompanion.Services;
using NightStalkerCompanion.ViewModels;
using NightStalkerCompanion.Views;
using PardApi;

namespace NightStalkerCompanion;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // The mock-up sets text in the iOS system face, so no font file is
                // registered for it — that is the default. Numbers are set in Menlo,
                // a system font referenced by name in the styles, which also needs no
                // bundle. OpenSans stays only as a fallback for hosts without SF.
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // The scope client is a singleton: it owns one HttpClient, and the device
        // does not like a new connection per screen.
        builder.Services.AddSingleton<IPardService>(_ => new PardService(ScopeHost));

        // One shared device state for every screen — see the comment on AppState.
        builder.Services.AddSingleton<AppState>();

        // Registered as "view model to view" pairs, with the lifetime following the screen
        // type, the way the reference Innowise.Music does it: a tab lives for the whole run of
        // the app, a push screen is rebuilt on every opening.
        //
        // Push screens cannot be singletons: ReticleViewModel holds TargetGroup, and one screen
        // shared by every opening would open the group you left ten screens ago.

        // Tabs.
        builder.Services.AddSingleton<DeviceViewModel>();
        builder.Services.AddSingleton<DevicePage>();

        builder.Services.AddSingleton<GalleryViewModel>();
        builder.Services.AddSingleton<GalleryPage>();

        // Screens that open on top of the tabs.
        builder.Services.AddTransient<LiveViewModel>();
        builder.Services.AddTransient<LiveViewPage>();

        builder.Services.AddTransient<ReticleViewModel>();
        builder.Services.AddTransient<ReticlePage>();

        builder.Services.AddTransient<BallisticsViewModel>();
        builder.Services.AddTransient<BallisticsPage>();

        builder.Services.AddTransient<UnitsViewModel>();
        builder.Services.AddTransient<UnitsPage>();

        // The Shell itself comes from the container as well: then it has the same dependencies
        // as everything else, and the routes are registered in one place.
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>
    /// Where the scope lives: 192.168.1.254, the access point the scope creates itself.
    ///
    /// A debug build may be pointed elsewhere by a launch argument, which is how the app runs
    /// against tools/mock_scope.py on a simulator — the only way the write paths can be watched
    /// at all without holding the scope in the hand:
    ///
    ///     xcrun simctl launch <device> com.companyname.nightstalkercompanion \
    ///         --args -PardHost 127.0.0.1:8080
    ///
    /// Debug only on purpose. A shipped build has one address it is allowed to talk to, and the
    /// fault line prints that address, so a wrong host is never silent.
    /// </summary>
    private static string ScopeHost
    {
#if DEBUG
        get
        {
            string? named = Foundation.NSUserDefaults.StandardUserDefaults.StringForKey("PardHost");
            return string.IsNullOrWhiteSpace(named) ? PardClient.DefaultHost : named;
        }
#else
        get => PardClient.DefaultHost;
#endif
    }
}
