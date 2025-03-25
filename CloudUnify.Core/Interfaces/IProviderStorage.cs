using CloudUnify.Core.Models;

namespace CloudUnify.Core.Interfaces;

public interface IProviderStorage {
    void SaveProvider(string providerId, string providerType, string name, string? userId = null, string? clientSecretsPath = null);
    void UpdateConnectionState(string providerId, bool isConnected, string? userId = null);
    ProviderInfo? GetProvider(string providerId);
    List<ProviderInfo> GetAllProviders();
    List<ProviderInfo> GetConnectedProviders();
    void RemoveProvider(string providerId);
}