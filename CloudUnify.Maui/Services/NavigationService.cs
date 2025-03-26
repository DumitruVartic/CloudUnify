using Microsoft.AspNetCore.Components;

namespace CloudUnify.Maui.Services;

public class NavigationService {
    private readonly NavigationManager _navigationManager;

    public NavigationService(NavigationManager navigationManager) {
        _navigationManager = navigationManager;
    }

    public string GetCurrentPath() {
        return _navigationManager.Uri;
    }

    public async Task NavigateToWelcomeAsync() {
        await Task.Run(() => _navigationManager.NavigateTo("/"));
    }

    public async Task NavigateToAddStorageAsync() {
        await Task.Run(() => _navigationManager.NavigateTo("/add-storage"));
    }

    public async Task NavigateToStoragesAsync() {
        await Task.Run(() => _navigationManager.NavigateTo("/storages"));
    }

    public async Task NavigateBackAsync() {
        await Task.Run(() => _navigationManager.NavigateTo(".."));
    }

    public async Task NavigateToAsync(string path) {
        await Task.Run(() => _navigationManager.NavigateTo(path));
    }

    // Synchronous methods for backward compatibility
    public void NavigateToWelcome() {
        _navigationManager.NavigateTo("/");
    }

    public void NavigateToAddStorage() {
        _navigationManager.NavigateTo("/add-storage");
    }

    public void NavigateToStorages() {
        _navigationManager.NavigateTo("/storages");
    }

    public void NavigateBack() {
        _navigationManager.NavigateTo("..");
    }

    public void NavigateTo(string path) {
        _navigationManager.NavigateTo(path);
    }
}