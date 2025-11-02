using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FhBrowser; // Убедитесь, что пространство имен совпадает

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Регистрация ваших страниц
        builder.Services.AddSingleton<BrowserPage>();
        builder.Services.AddSingleton<App>();

        // Эквивалент вашего Mutex для Windows
#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(wnd => wnd.OnLaunched((app, args) =>
            {
                if (!App.IsSingleInstance())
                {
                    // TODO: Найти и активировать существующее окно
                    app.Exit(); // Закрываем новый экземпляр
                }
            }));
        });
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
