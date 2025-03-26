using CloudUnify.Core;
using CloudUnify.Core.Interfaces;
using CloudUnify.Core.Storage;
using CloudUnify.Maui.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace CloudUnify.Maui;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddScoped<NavigationService>();
        builder.Services.AddMudServices();

        // Register CloudUnify services
        var appDataPath = Path.Combine(FileSystem.AppDataDirectory, "providers.json");
        builder.Services.AddSingleton<IProviderStorage>(sp => new ProviderStorage(appDataPath));
        builder.Services.AddScoped<CloudUnifyManager>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}