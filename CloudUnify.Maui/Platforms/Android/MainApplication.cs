﻿namespace CloudUnify.Maui;

[Application]
public class MainApplication : MauiApplication {
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership) {
    }

    protected override MauiApp CreateMauiApp() {
        return MauiProgram.CreateMauiApp();
    }
}