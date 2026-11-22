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

        builder.Services.AddSingleton<DeviceViewModel>();
        builder.Services.AddSingleton<DevicePage>();

        builder.Services.AddSingleton<LiveViewModel>();
        builder.Services.AddTransient<LiveViewPage>();

        builder.Services.AddSingleton<ReticleViewModel>();
        builder.Services.AddSingleton<ReticlePage>();

        builder.Services.AddSingleton<BallisticsViewModel>();
        builder.Services.AddSingleton<BallisticsPage>();

        builder.Services.AddSingleton<UnitsViewModel>();
        builder.Services.AddSingleton<UnitsPage>();

        builder.Services.AddSingleton<GalleryViewModel>();
        builder.Services.AddSingleton<GalleryPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
