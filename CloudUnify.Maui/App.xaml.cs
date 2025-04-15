using CloudUnify.Core;

namespace CloudUnify.Maui;

public partial class App : Application {
    private readonly CloudUnifyManager _cloudUnifyManager;
    private Window? _mainWindow;

    public App(CloudUnifyManager cloudUnifyManager) {
        InitializeComponent();
        _cloudUnifyManager = cloudUnifyManager;
        _ = AutoConnectProvidersAsync();
    }

    private async Task AutoConnectProvidersAsync() {
        try {
            await _cloudUnifyManager.AutoConnectProvidersAsync();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error auto-connecting providers: {ex.Message}");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState) {
        _mainWindow = new Window(new MainPage()) { Title = "CloudUnify.Maui" };
        _mainWindow.Activated += OnWindowActivated;
        return _mainWindow;
    }

    private async void OnWindowActivated(object? sender, EventArgs e) {
        await AutoConnectProvidersAsync();
    }

    protected override void OnResume() {
        base.OnResume();
        _ = AutoConnectProvidersAsync();
    }
}