using CloudUnify.Core.Authentication;
using CloudUnify.Core.Extensions;
using CloudUnify.Core.Providers;

namespace CloudUnify.Core;

public class CloudUnifyManager {
    private readonly Dictionary<string, IAuthProvider> _authHelpers = new();
    private readonly CloudUnify _cloudUnify = new();
    private readonly Dictionary<string, ICloudProvider> _providers = new();

    public async Task<(string ProviderId, bool Success)> ConnectGoogleDriveAsync(
        string clientSecretsPath,
        string applicationName,
        string dataStorePath,
        string userId,
        string? providerId = null) {
        try {
            // Generate a new provider ID if not provided
            providerId ??= Guid.NewGuid().ToString();

            var authHelper = new GoogleAuthProvider(
                clientSecretsPath,
                applicationName,
                dataStorePath
            );

            _authHelpers[providerId] = authHelper;

            // Create and connect the provider
            var provider = new GoogleDriveProvider(providerId, applicationName);
            var accessToken = await authHelper.AuthenticateAsync(userId);
            var connected = await provider.ConnectAsync(accessToken);

            if (connected) {
                _providers[providerId] = provider;
                _cloudUnify.RegisterProvider(provider);
                return (providerId, true);
            }

            return (providerId, false);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error connecting to Google Drive: {ex.Message}");
            return (providerId ?? string.Empty, false);
        }
    }

    public async Task<(string ProviderId, bool Success)> ConnectOneDriveAsync(
        string clientSecretsPath,
        string applicationName,
        string dataStorePath,
        string userId,
        string? providerId = null) {
        try {
            // Generate a new provider ID if not provided
            providerId ??= Guid.NewGuid().ToString();

            var authHelper = new OneDriveAuthProvider(
                clientSecretsPath,
                applicationName,
                dataStorePath
            );

            _authHelpers[providerId] = authHelper;

            // Create and connect the provider
            var provider = new OneDriveProvider(providerId, applicationName);
            var accessToken = await authHelper.AuthenticateAsync(userId);
            var connected = await provider.ConnectAsync(accessToken);

            if (connected) {
                _providers[providerId] = provider;
                _cloudUnify.RegisterProvider(provider);
                return (providerId, true);
            }

            return (providerId, false);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error connecting to OneDrive: {ex.Message}");
            return (providerId ?? string.Empty, false);
        }
    }

    public bool HasProvider(string providerId) {
        return _providers.ContainsKey(providerId);
    }

    public IReadOnlyList<string> GetProviderIds() {
        return new List<string>(_providers.Keys);
    }

    public async Task DisconnectProviderAsync(string providerId, string userId) {
        if (!_providers.TryGetValue(providerId, out var provider)) throw new ArgumentException($"Provider with ID {providerId} not found");

        await provider.DisconnectAsync();

        if (_authHelpers.TryGetValue(providerId, out var authHelperObj)) {
            if (authHelperObj is GoogleAuthProvider GoogleAuthProvider)
                await GoogleAuthProvider.RevokeTokenAsync(userId);
            else if (authHelperObj is OneDriveAuthProvider oneDriveAuthHelper) await oneDriveAuthHelper.RevokeTokenAsync(userId);
        }
    }

    // Expose CloudUnify methods

    public Task<List<UnifiedCloudFile>> ListAllFilesAsync(string path = "/") {
        return _cloudUnify.ListAllFilesAsync(path);
    }

    public Task<UnifiedCloudFile?> GetFileAsync(string fileId, string providerId) {
        return _cloudUnify.GetFileAsync(fileId, providerId);
    }

    public Task<byte[]> DownloadFileAsync(string fileId, string providerId) {
        return _cloudUnify.DownloadFileAsync(fileId, providerId);
    }

    public Task<UnifiedCloudFile> UploadFileAsync(byte[] content, string fileName, string path, string providerId) {
        return _cloudUnify.UploadFileAsync(content, fileName, path, providerId);
    }

    public Task DeleteFileAsync(string fileId, string providerId) {
        return _cloudUnify.DeleteFileAsync(fileId, providerId);
    }

    public Task<UnifiedCloudFile> MoveFileAsync(string fileId, string newPath, string providerId) {
        return _cloudUnify.MoveFileAsync(fileId, newPath, providerId);
    }

    public Task<UnifiedCloudFile> CopyFileAsync(string fileId, string newPath, string providerId) {
        return _cloudUnify.CopyFileAsync(fileId, newPath, providerId);
    }

    public Task<UnifiedCloudFile> RenameFileAsync(string fileId, string newName, string providerId) {
        return _cloudUnify.RenameFileAsync(fileId, newName, providerId);
    }

    public Task<List<CloudStorageInfo>> GetStorageInfoAsync() {
        return _cloudUnify.GetStorageInfoAsync();
    }

    public Task<UnifiedCloudFile> CopyFileBetweenProvidersAsync(
        string sourceFileId,
        string sourceProviderId,
        string destinationPath,
        string destinationProviderId) {
        return _cloudUnify.CopyFileBetweenProvidersAsync(
            sourceFileId, sourceProviderId,
            destinationPath, destinationProviderId);
    }

    // Add search functionality directly to the manager
    public Task<List<UnifiedCloudFile>> SearchFilesAsync(string searchTerm, SearchOptions? options = null) {
        return _cloudUnify.SearchFilesAsync(searchTerm, options);
    }
}