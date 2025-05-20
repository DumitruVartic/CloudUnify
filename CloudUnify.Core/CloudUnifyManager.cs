using System.Diagnostics;
using CloudUnify.Core.Authentication;
using CloudUnify.Core.Extensions;
using CloudUnify.Core.Interfaces;
using CloudUnify.Core.Models;
using CloudUnify.Core.Providers;

namespace CloudUnify.Core;

public class CloudUnifyManager {
    private const string ApplicationName = "CloudUnify";
    private const string TokenStorePath = "token_store";
    private readonly Dictionary<string, IAuthProvider> _authHelpers = new();
    private readonly CloudUnify _cloudUnify = new();
    private readonly Dictionary<string, ICloudProvider> _providers = new();
    private readonly IProviderStorage _providerStorage;

    public CloudUnifyManager(IProviderStorage providerStorage) {
        _providerStorage = providerStorage;
    }

    public async Task<(string ProviderId, bool Success)> ConnectGoogleDriveAsync(
        string clientSecretsPath,
        string applicationName,
        string dataStorePath,
        string userId,
        string? providerId = null) {
        try {
            // Generate a new provider ID if not provided
            providerId ??= Guid.NewGuid().ToString();
            Debug.WriteLine($"Connecting Google Drive with ID: {providerId}");

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
                Debug.WriteLine($"Provider {providerId} connected successfully");
                
                // Get account info from the provider
                var accountInfo = await provider.GetAccountInfoAsync();
                var displayName = accountInfo?.DisplayName ?? "Google Drive";
                var email = accountInfo?.Email ?? string.Empty;

                // Check if we already have a provider with this email
                var googleProviders = _providers.Values.OfType<GoogleDriveProvider>().ToList();
                foreach (var existingProvider in googleProviders) {
                    var existingInfo = await existingProvider.GetAccountInfoAsync();
                    if (existingInfo?.Email == email) {
                        Debug.WriteLine($"Provider with email {email} already exists. Disconnecting old provider.");
                        await DisconnectProviderAsync(existingProvider.Id, userId);
                        break;
                    }
                }

                _providers[providerId] = provider;
                _cloudUnify.RegisterProvider(provider);
                _providerStorage.SaveProvider(providerId, "GoogleDrive", displayName, userId, clientSecretsPath);

                // Verify registration
                var providers = _cloudUnify.GetProviders();
                Debug.WriteLine($"CloudUnify now has {providers.Count} providers");
                foreach (var p in providers) Debug.WriteLine($"- Provider: {p.Name} (ID: {p.Id})");

                return (providerId, true);
            }

            Debug.WriteLine($"Provider {providerId} failed to connect");
            return (providerId, false);
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error connecting to Google Drive: {ex}");
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
                // Get account info from the provider
                var accountInfo = await provider.GetAccountInfoAsync();
                var displayName = accountInfo?.DisplayName ?? "OneDrive";
                var email = accountInfo?.Email ?? string.Empty;

                // Check if we already have a provider with this email
                var oneDriveProviders = _providers.Values.OfType<OneDriveProvider>().ToList();
                foreach (var existingProvider in oneDriveProviders) {
                    var existingInfo = await existingProvider.GetAccountInfoAsync();
                    if (existingInfo?.Email == email) {
                        Debug.WriteLine($"Provider with email {email} already exists. Disconnecting old provider.");
                        await DisconnectProviderAsync(existingProvider.Id, userId);
                        break;
                    }
                }

                _providers[providerId] = provider;
                _cloudUnify.RegisterProvider(provider);
                _providerStorage.SaveProvider(providerId, "OneDrive", displayName, userId, clientSecretsPath);
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
        // First update the connection state in storage
        _providerStorage.UpdateConnectionState(providerId, false);

        // Then try to disconnect the provider if it exists in the dictionary
        if (_providers.TryGetValue(providerId, out var provider)) {
            await provider.DisconnectAsync();

            if (_authHelpers.TryGetValue(providerId, out var authHelperObj)) {
                if (authHelperObj is GoogleAuthProvider GoogleAuthProvider)
                    await GoogleAuthProvider.RevokeTokenAsync(userId);
                else if (authHelperObj is OneDriveAuthProvider oneDriveAuthHelper)
                    await oneDriveAuthHelper.RevokeTokenAsync(userId);
            }

            // Remove from dictionaries
            _providers.Remove(providerId);
            _authHelpers.Remove(providerId);
            _cloudUnify.UnregisterProvider(providerId);
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

    public async Task AutoConnectProvidersAsync() {
        var connectedProviders = _providerStorage.GetConnectedProviders();

        if (connectedProviders.Count == 0) return;

        foreach (var provider in connectedProviders) {
            if (string.IsNullOrEmpty(provider.UserId)) continue;

            try {
                var clientSecretsPath = provider.ClientSecretsPath;
                if (string.IsNullOrEmpty(clientSecretsPath)) {
                    Debug.WriteLine($"Client secrets path is missing for provider {provider.Name}");
                    continue;
                }

                if (!File.Exists(clientSecretsPath)) {
                    Debug.WriteLine(
                        $"Client secrets file not found at {clientSecretsPath} for provider {provider.Name}");
                    continue;
                }

                bool success;
                if (provider.Type == "GoogleDrive") {
                    var (_, connected) = await ConnectGoogleDriveAsync(
                        clientSecretsPath,
                        ApplicationName,
                        TokenStorePath,
                        provider.UserId,
                        provider.Id
                    );
                    success = connected;
                }
                else if (provider.Type == "OneDrive") {
                    var (_, connected) = await ConnectOneDriveAsync(
                        clientSecretsPath,
                        ApplicationName,
                        TokenStorePath,
                        provider.UserId,
                        provider.Id
                    );
                    success = connected;
                }
                else {
                    Debug.WriteLine($"Unknown provider type: {provider.Type}");
                    continue;
                }

                _providerStorage.UpdateConnectionState(provider.Id, success);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error reconnecting to {provider.Name}: {ex.Message}");
                _providerStorage.UpdateConnectionState(provider.Id, false);
            }
        }
    }

    public async Task<bool> ConnectProviderAsync(StorageProvider providerType) {
        try {
            Debug.WriteLine($"Attempting to connect provider: {providerType}");

            var clientSecretsPath = GetClientSecretsPath(providerType);
            if (string.IsNullOrEmpty(clientSecretsPath)) {
                Debug.WriteLine("Client secrets path not found");
                return false;
            }

            var userId = "default_user"; // We'll use a default user for now
            var success = false;
            string? providerId = null;

            switch (providerType) {
                case StorageProvider.GoogleDrive:
                    (providerId, success) = await ConnectGoogleDriveAsync(
                        clientSecretsPath,
                        ApplicationName,
                        TokenStorePath,
                        userId
                    );
                    break;

                case StorageProvider.OneDrive:
                    (providerId, success) = await ConnectOneDriveAsync(
                        clientSecretsPath,
                        ApplicationName,
                        TokenStorePath,
                        userId
                    );
                    break;
            }

            if (success) {
                Debug.WriteLine($"Provider connected successfully. ID: {providerId}");
                // Verify provider registration
                if (_providers.ContainsKey(providerId)) {
                    var provider = _providers[providerId];
                    Debug.WriteLine($"Provider exists in _providers dictionary: {provider.Name}");
                }
                else {
                    Debug.WriteLine("Warning: Provider not found in _providers dictionary");
                }

                // Verify CloudUnify registration
                var cloudUnifyProviders = await ListAllFilesAsync();
                Debug.WriteLine(
                    $"CloudUnify has {cloudUnifyProviders.Select(f => f.ProviderId).Distinct().Count()} registered providers");
            }
            else {
                Debug.WriteLine("Provider connection failed");
            }

            return success;
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error connecting provider: {ex}");
            return false;
        }
    }

    private string GetClientSecretsPath(StorageProvider providerType) {
        // For development, we'll look for client_secrets.json in the application directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var fileName = providerType switch {
            StorageProvider.GoogleDrive => "client_secrets_google.json",
            StorageProvider.OneDrive => "client_secrets_onedrive.json",
            _ => "client_secrets.json"
        };

        var path = Path.Combine(baseDir, fileName);
        Debug.WriteLine($"Looking for client secrets at: {path}");
        return File.Exists(path) ? path : string.Empty;
    }

    public async Task<IEnumerable<StorageProviderInfo>> GetConnectedProvidersAsync() {
        var providerIds = GetProviderIds();
        var storageInfo = await GetStorageInfoAsync();

        return providerIds.Select(id => {
            var info = storageInfo.FirstOrDefault(s => s.ProviderId == id);
            return new StorageProviderInfo {
                ProviderId = id,
                ProviderType = GetProviderType(id),
                AccountName = info?.UserEmail ?? "Unknown",
                UsedSpace = info?.UsedSpace ?? 0,
                TotalSpace = info?.TotalSpace ?? 0
            };
        });
    }

    private StorageProvider GetProviderType(string providerId) {
        if (_providers.TryGetValue(providerId, out var provider))
            return provider switch {
                GoogleDriveProvider => StorageProvider.GoogleDrive,
                OneDriveProvider => StorageProvider.OneDrive,
                _ => StorageProvider.Unknown
            };
        return StorageProvider.Unknown;
    }

    public async Task DisconnectProviderAsync(StorageProvider providerType) {
        var providerId = _providers.FirstOrDefault(p => GetProviderType(p.Key) == providerType).Key;
        if (string.IsNullOrEmpty(providerId))
            throw new ArgumentException($"No connected provider found for type {providerType}");

        await DisconnectProviderAsync(providerId, "default_user"); // TODO: Get actual user ID from authentication
    }

    public async Task<bool> ReconnectProviderAsync(string providerId) {
        try {
            var provider = _providerStorage.GetProvider(providerId);
            if (provider == null) {
                Console.WriteLine($"Provider with ID {providerId} not found");
                return false;
            }

            if (string.IsNullOrEmpty(provider.ClientSecretsPath)) {
                Console.WriteLine($"Client secrets path is missing for provider {providerId}");
                return false;
            }

            if (string.IsNullOrEmpty(provider.UserId)) {
                Console.WriteLine($"User ID is missing for provider {providerId}");
                return false;
            }

            bool success;
            if (provider.Type == "GoogleDrive") {
                var (_, connected) = await ConnectGoogleDriveAsync(
                    provider.ClientSecretsPath,
                    ApplicationName,
                    TokenStorePath,
                    provider.UserId,
                    provider.Id
                );
                success = connected;
            }
            else if (provider.Type == "OneDrive") {
                var (_, connected) = await ConnectOneDriveAsync(
                    provider.ClientSecretsPath,
                    ApplicationName,
                    TokenStorePath,
                    provider.UserId,
                    provider.Id
                );
                success = connected;
            }
            else {
                Console.WriteLine($"Unknown provider type: {provider.Type}");
                return false;
            }

            _providerStorage.UpdateConnectionState(providerId, success);
            return success;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error reconnecting provider: {ex.Message}");
            _providerStorage.UpdateConnectionState(providerId, false);
            return false;
        }
    }

    public async Task RemoveProviderAsync(string providerId) {
        try {
            // First disconnect the provider if it's connected
            if (_providers.ContainsKey(providerId)) {
                await DisconnectProviderAsync(providerId,
                    "default_user"); // TODO: Get actual user ID from authentication
                _providers.Remove(providerId);
                _cloudUnify.UnregisterProvider(providerId);
            }

            // Remove auth helper if it exists
            if (_authHelpers.ContainsKey(providerId)) _authHelpers.Remove(providerId);

            // Remove from storage
            _providerStorage.RemoveProvider(providerId);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error removing provider {providerId}: {ex.Message}");
            throw;
        }
    }
}