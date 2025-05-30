using System.Collections.ObjectModel;
using System.Diagnostics;
using CloudUnify.Core;
using CloudUnify.Core.Models;
using CloudUnify.Maui.Models;
using CloudUnify.Maui.Services;

namespace CloudUnify.Maui.ViewModels;

public class FileSystemViewModel : BaseViewModel {
    private readonly CloudUnifyManager _cloudUnifyManager;
    private readonly CloudFileSystemService _fileSystemService;
    private readonly SecureStorageService _secureStorageService;
    private ObservableCollection<StorageProviderInfo> _availableProviders;
    private List<(string Name, string Path)> _breadcrumbItems;
    private string _currentPath = "/";
    private string? _currentProviderId;
    private bool _isGridView = true;
    private bool _isMultiSelect;
    private ObservableCollection<FileSystemItem> _items;
    private List<FileSystemItem> _selectedItems;

    public FileSystemViewModel(
        CloudFileSystemService fileSystemService,
        CloudUnifyManager cloudUnifyManager,
        SecureStorageService secureStorageService) {
        _fileSystemService = fileSystemService;
        _cloudUnifyManager = cloudUnifyManager;
        _secureStorageService = secureStorageService;
        _items = new ObservableCollection<FileSystemItem>();
        _breadcrumbItems = new List<(string Name, string Path)>();
        _selectedItems = new List<FileSystemItem>();
        _availableProviders = new ObservableCollection<StorageProviderInfo>();

        LoadProvidersAsync().ConfigureAwait(false);
    }

    public ObservableCollection<StorageProviderInfo> AvailableProviders {
        get => _availableProviders;
        set => SetProperty(ref _availableProviders, value);
    }

    public string? CurrentProviderId {
        get => _currentProviderId;
        set {
            if (value != null && !AvailableProviders.Any(p => p.ProviderId == value)) {
                Debug.WriteLine($"Attempted to set invalid provider ID: {value}");
                return;
            }

            if (SetProperty(ref _currentProviderId, value)) {
                LoadCurrentFolder();
                UpdateBreadcrumbs();
            }
        }
    }

    public ObservableCollection<FileSystemItem> Items {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public List<(string Name, string Path)> BreadcrumbItems {
        get => _breadcrumbItems;
        set => SetProperty(ref _breadcrumbItems, value);
    }

    public List<FileSystemItem> SelectedItems {
        get => _selectedItems;
        set => SetProperty(ref _selectedItems, value);
    }

    public string CurrentPath {
        get => _currentPath;
        set => SetProperty(ref _currentPath, value);
    }

    public bool IsGridView {
        get => _isGridView;
        set => SetProperty(ref _isGridView, value);
    }

    public bool IsMultiSelect {
        get => _isMultiSelect;
        set => SetProperty(ref _isMultiSelect, value);
    }

    private async Task LoadProvidersAsync() {
        try {
            var providers = await _cloudUnifyManager.GetConnectedProvidersAsync();
            AvailableProviders.Clear();
            foreach (var provider in providers) AvailableProviders.Add(provider);
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error loading providers: {ex}");
        }
    }

    public async Task<bool> ConnectProviderAsync(StorageProvider provider) {
        try {
            var clientSecrets = provider switch {
                StorageProvider.GoogleDrive => await _secureStorageService.GetGoogleDriveSecretsAsync(),
                StorageProvider.OneDrive => await _secureStorageService.GetOneDriveSecretsAsync(),
                _ => null
            };

            if (string.IsNullOrEmpty(clientSecrets)) {
                Debug.WriteLine("Client secrets not found in secure storage");
                return false;
            }

            // Create a temporary file for the client secrets
            var tempPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempPath, clientSecrets);

            try {
                var (providerId, success) = provider switch {
                    StorageProvider.GoogleDrive => await _cloudUnifyManager.ConnectGoogleDriveAsync(
                        tempPath,
                        "CloudUnify",
                        "token_store",
                        "default_user"
                    ),
                    StorageProvider.OneDrive => await _cloudUnifyManager.ConnectOneDriveAsync(
                        tempPath,
                        "CloudUnify",
                        "token_store",
                        "default_user"
                    ),
                    _ => (string.Empty, false)
                };

                if (success) {
                    await LoadProvidersAsync();
                    return true;
                }

                return false;
            }
            finally {
                // Clean up the temporary file
                try {
                    File.Delete(tempPath);
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error deleting temporary file: {ex.Message}");
                }
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error connecting provider: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectProviderAsync(string providerId) {
        try {
            await _cloudUnifyManager.DisconnectProviderAsync(providerId, "default_user");
            await LoadProvidersAsync();
            if (_currentProviderId == providerId) {
                _currentProviderId = null;
                LoadCurrentFolder();
                UpdateBreadcrumbs();
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error disconnecting provider: {ex}");
        }
    }

    public async Task NavigateToFolder(string path, string? providerId = null) {
        try {
            CurrentPath = path;
            if (providerId != null) _currentProviderId = providerId;

            await LoadCurrentFolder();
            UpdateBreadcrumbs();
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error navigating to folder: {ex}");
            // Consider showing an error message to the user here
        }
    }

    private void UpdateBreadcrumbs() {
        var newBreadcrumbs = new List<(string Name, string Path)>();

        if (_currentProviderId != null) {
            var provider = AvailableProviders.FirstOrDefault(p => p.ProviderId == _currentProviderId);
            newBreadcrumbs.Add((provider?.AccountName ?? _currentProviderId, "/"));
        }
        else {
            newBreadcrumbs.Add(("Home", "/"));
        }

        if (CurrentPath != "/") {
            var parts = CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";
            foreach (var part in parts) {
                currentPath += "/" + part;
                newBreadcrumbs.Add((part, currentPath));
            }
        }

        BreadcrumbItems = newBreadcrumbs;
    }

    public async Task LoadCurrentFolder() {
        if (IsBusy) return; // Prevent multiple concurrent loads

        try {
            IsBusy = true;
            Items.Clear(); // Clear items immediately to show loading state

            if (_currentProviderId != null) {
                var provider = AvailableProviders.FirstOrDefault(p => p.ProviderId == _currentProviderId);
                if (provider == null) {
                    Debug.WriteLine($"Provider {_currentProviderId} not found in available providers");
                    return;
                }

                Debug.WriteLine($"Loading files for provider: {provider.AccountName} ({_currentProviderId})");
            }
            else {
                Debug.WriteLine("Loading files in unified view");
            }

            var files = await _cloudUnifyManager.ListAllFilesAsync(CurrentPath);
            Debug.WriteLine($"Found {files.Count} files from CloudUnifyManager");

            if (_currentProviderId != null) {
                files = files.Where(f => f.ProviderId == _currentProviderId).ToList();
                Debug.WriteLine($"Filtered to {files.Count} files for provider {_currentProviderId}");
            }

            foreach (var file in files) {
                Debug.WriteLine($"Adding file: {file.Name} (Provider: {file.ProviderName})");
                Items.Add(new FileSystemItem {
                    Id = file.Id,
                    Name = file.Name,
                    Path = file.Path,
                    Size = file.Size,
                    CreatedAt = file.CreatedAt,
                    ModifiedAt = file.ModifiedAt,
                    IsFolder = file.IsFolder,
                    Provider = file.ProviderName,
                    MimeType = file.MimeType
                });
            }

            Debug.WriteLine($"Added {Items.Count} items to the view");
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error loading folder: {ex}");
            // Consider showing an error message to the user here
        }
        finally {
            IsBusy = false;
        }
    }

    public async Task NavigateUp() {
        if (CurrentPath == "/") return;

        var parentPath = Path.GetDirectoryName(CurrentPath)?.Replace("\\", "/") ?? "/";
        await NavigateToFolder(parentPath, _currentProviderId);
    }

    public void ToggleItemSelection(FileSystemItem item) {
        if (_selectedItems.Contains(item))
            _selectedItems.Remove(item);
        else
            _selectedItems.Add(item);
        OnPropertyChanged(nameof(SelectedItems));
    }

    public void ClearSelection() {
        _selectedItems.Clear();
        OnPropertyChanged(nameof(SelectedItems));
    }

    public void ToggleViewMode() {
        IsGridView = !IsGridView;
    }

    public async Task DeleteSelected() {
        foreach (var item in _selectedItems.ToList())
            try {
                await _fileSystemService.DeleteAsync(item.Id, item.Provider);
                Items.Remove(item);
                _selectedItems.Remove(item);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error deleting file {item.Name}: {ex}");
            }

        OnPropertyChanged(nameof(SelectedItems));
    }

    public async Task CreateNewFolder(string name) {
        try {
            if (_currentProviderId == null) return;

            var folder = await _fileSystemService.CreateFolderAsync(name, CurrentPath, _currentProviderId);
            Items.Add(new FileSystemItem {
                Id = folder.Id,
                Name = folder.Name,
                Path = folder.Path,
                IsFolder = true,
                Provider = folder.ProviderName
            });
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error creating folder: {ex}");
        }
    }

    public async Task RenameItem(FileSystemItem item, string newName) {
        try {
            var renamedFile = await _fileSystemService.RenameAsync(item.Id, newName, item.Provider);
            var index = Items.IndexOf(item);
            if (index != -1)
                Items[index] = new FileSystemItem {
                    Id = renamedFile.Id,
                    Name = renamedFile.Name,
                    Path = renamedFile.Path,
                    Size = renamedFile.Size,
                    CreatedAt = renamedFile.CreatedAt,
                    ModifiedAt = renamedFile.ModifiedAt,
                    IsFolder = renamedFile.IsFolder,
                    Provider = renamedFile.ProviderName,
                    MimeType = renamedFile.MimeType
                };
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error renaming file {item.Name}: {ex}");
        }
    }

    public async Task SwitchToUnifiedView() {
        _currentProviderId = null;
        CurrentPath = "/";
        await LoadCurrentFolder();
        UpdateBreadcrumbs();
    }
}