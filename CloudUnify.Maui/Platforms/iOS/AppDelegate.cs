using Foundation;

namespace CloudUnify.Maui;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate {
    protected override MauiApp CreateMauiApp() {
        return MauiProgram.CreateMauiApp();
    }
}