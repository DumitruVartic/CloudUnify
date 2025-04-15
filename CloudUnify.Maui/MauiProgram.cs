using CloudUnify.Core;
using CloudUnify.Core.Interfaces;
using CloudUnify.Core.Storage;
using CloudUnify.Maui.Services;
using CloudUnify.Maui.ViewModels;
using Microsoft.Extensions.Logging;

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
        builder.Services.AddSingleton<SecureStorageService>();

        // Register CloudUnify services
        var appDataPath = Path.Combine(FileSystem.AppDataDirectory, "providers.json");
        builder.Services.AddSingleton<IProviderStorage>(sp => new ProviderStorage(appDataPath));
        builder.Services.AddSingleton<CloudUnifyManager>();
        builder.Services.AddSingleton<App>();

        // Register CloudUnify core services
        builder.Services.AddSingleton<Core.CloudUnify>();

        // Register file system services
        builder.Services.AddSingleton<FileSystemService>();
        builder.Services.AddScoped<FileSystemViewModel>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}