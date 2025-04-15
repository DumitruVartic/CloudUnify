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
        _navigationManager.NavigateTo("/");
        await Task.CompletedTask;
    }

    public async Task NavigateToAddStorageAsync() {
        _navigationManager.NavigateTo("/add-storage");
        await Task.CompletedTask;
    }

    public async Task NavigateToStoragesAsync() {
        _navigationManager.NavigateTo("/storages");
        await Task.CompletedTask;
    }

    public async Task NavigateToFileSystemAsync() {
        _navigationManager.NavigateTo("/filesystem");
        await Task.CompletedTask;
    }

    public async Task NavigateBackAsync() {
        _navigationManager.NavigateTo(_navigationManager.BaseUri);
        await Task.CompletedTask;
    }

    public async Task NavigateToAsync(string path) {
        _navigationManager.NavigateTo(path);
        await Task.CompletedTask;
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

    public void NavigateToFileSystem() {
        _navigationManager.NavigateTo("/filesystem");
    }

    public void NavigateBack() {
        _navigationManager.NavigateTo(_navigationManager.BaseUri);
    }

    public void NavigateTo(string path) {
        _navigationManager.NavigateTo(path);
    }
}