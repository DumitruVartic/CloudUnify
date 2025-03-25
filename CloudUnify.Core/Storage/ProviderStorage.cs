using System.Text.Json;
using CloudUnify.Core.Interfaces;
using CloudUnify.Core.Models;

namespace CloudUnify.Core.Storage;

public class ProviderStorage : IProviderStorage {
    private readonly string _storagePath;
    private Dictionary<string, ProviderInfo> _providers = new();

    public ProviderStorage(string storagePath) {
        _storagePath = storagePath;
        LoadProviders();
    }

    public void SaveProvider(string providerId, string providerType, string name, string? userId = null, string? clientSecretsPath = null) {
        if (_providers.TryGetValue(providerId, out var existingProvider)) {
            // Update existing provider
            existingProvider.Name = name;
            existingProvider.Type = providerType;

            if (clientSecretsPath != null) existingProvider.ClientSecretsPath = clientSecretsPath;

            if (userId != null) {
                existingProvider.UserId = userId;
                existingProvider.IsConnected = true;
                existingProvider.LastConnected = DateTime.UtcNow;
            }
        }
        else {
            // Create new provider
            _providers[providerId] = new ProviderInfo {
                Id = providerId,
                Type = providerType,
                Name = name,
                AddedAt = DateTime.UtcNow,
                UserId = userId,
                IsConnected = userId != null,
                LastConnected = userId != null ? DateTime.UtcNow : null,
                ClientSecretsPath = clientSecretsPath
            };
        }

        SaveProviders();
    }

    public void UpdateConnectionState(string providerId, bool isConnected, string? userId = null) {
        if (_providers.TryGetValue(providerId, out var provider)) {
            provider.IsConnected = isConnected;
            if (isConnected) {
                provider.LastConnected = DateTime.UtcNow;
                if (userId != null) provider.UserId = userId;
            }

            SaveProviders();
        }
    }

    public ProviderInfo? GetProvider(string providerId) {
        return _providers.TryGetValue(providerId, out var provider) ? provider : null;
    }

    public List<ProviderInfo> GetAllProviders() {
        return new List<ProviderInfo>(_providers.Values);
    }

    public List<ProviderInfo> GetConnectedProviders() {
        var connectedProviders = new List<ProviderInfo>();
        foreach (var provider in _providers.Values)
            if (provider.IsConnected && !string.IsNullOrEmpty(provider.UserId))
                connectedProviders.Add(provider);

        return connectedProviders;
    }

    public void RemoveProvider(string providerId) {
        if (_providers.ContainsKey(providerId)) {
            _providers.Remove(providerId);
            SaveProviders();
        }
    }

    private void LoadProviders() {
        try {
            if (File.Exists(_storagePath)) {
                var json = File.ReadAllText(_storagePath);
                _providers = JsonSerializer.Deserialize<Dictionary<string, ProviderInfo>>(json)
                             ?? new Dictionary<string, ProviderInfo>();
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error loading providers: {ex.Message}");
            _providers = new Dictionary<string, ProviderInfo>();
        }
    }

    private void SaveProviders() {
        try {
            var json = JsonSerializer.Serialize(_providers);
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error saving providers: {ex.Message}");
        }
    }
}