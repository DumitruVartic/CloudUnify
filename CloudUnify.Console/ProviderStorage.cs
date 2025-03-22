using System.Text.Json;

namespace CloudUnify.Console;

public class ProviderStorage {
    private readonly string _storagePath;
    private Dictionary<string, ProviderInfo> _providers = new();

    public ProviderStorage(string storagePath) {
        _storagePath = storagePath;
        LoadProviders();
    }

    public void SaveProvider(string providerId, string providerType, string name) {
        _providers[providerId] = new ProviderInfo {
            Id = providerId,
            Type = providerType,
            Name = name,
            AddedAt = DateTime.UtcNow
        };

        SaveProviders();
    }

    public ProviderInfo? GetProvider(string providerId) {
        return _providers.TryGetValue(providerId, out var provider) ? provider : null;
    }

    public List<ProviderInfo> GetAllProviders() {
        return new List<ProviderInfo>(_providers.Values);
    }

    public void RemoveProvider(string providerId) {
        if (!_providers.ContainsKey(providerId)) return;
        _providers.Remove(providerId);
        SaveProviders();
    }

    private void LoadProviders() {
        try {
            if (!File.Exists(_storagePath)) return;
            var json = File.ReadAllText(_storagePath);
            _providers = JsonSerializer.Deserialize<Dictionary<string, ProviderInfo>>(json)
                         ?? new Dictionary<string, ProviderInfo>();
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error loading providers: {ex.Message}");
            _providers = new Dictionary<string, ProviderInfo>();
        }
    }

    private void SaveProviders() {
        try {
            var json = JsonSerializer.Serialize(_providers);
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error saving providers: {ex.Message}");
        }
    }
}

public class ProviderInfo {
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
}