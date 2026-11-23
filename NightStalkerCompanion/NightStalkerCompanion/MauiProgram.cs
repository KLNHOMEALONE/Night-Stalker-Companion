using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NightStalkerCompanion.Services;
using NightStalkerCompanion.ViewModels;
using NightStalkerCompanion.Views;

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
        builder.Services.AddSingleton<IPardService>(_ => new PardService());

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
}
