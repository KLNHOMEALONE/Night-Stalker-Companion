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

        // Регистрация парами «вьюмодель → вьюха», жизнь по типу экрана — как в
        // эталонном Innowise.Music: вкладка живёт всё время работы приложения, а
        // push-экран создаётся заново на каждое открытие.
        //
        // Push-экраны не могут быть синглтонами: ReticleViewModel держит TargetGroup,
        // и общий на все открытия экран открыл бы группу, от которой ты ушёл десять
        // экранов назад.

        // Вкладки.
        builder.Services.AddSingleton<DeviceViewModel>();
        builder.Services.AddSingleton<DevicePage>();

        builder.Services.AddSingleton<GalleryViewModel>();
        builder.Services.AddSingleton<GalleryPage>();

        // Экраны, которые открываются поверх вкладок.
        builder.Services.AddTransient<LiveViewModel>();
        builder.Services.AddTransient<LiveViewPage>();

        builder.Services.AddTransient<ReticleViewModel>();
        builder.Services.AddTransient<ReticlePage>();

        builder.Services.AddTransient<BallisticsViewModel>();
        builder.Services.AddTransient<BallisticsPage>();

        builder.Services.AddTransient<UnitsViewModel>();
        builder.Services.AddTransient<UnitsPage>();

        // Сам Shell тоже из контейнера: тогда у него есть те же зависимости, что у
        // остальных, и маршруты регистрируются в одном месте.
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
