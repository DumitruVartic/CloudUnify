namespace CloudUnify.Core.Models;

public class StorageProviderInfo {
    public string ProviderId { get; set; } = string.Empty;
    public StorageProvider ProviderType { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public long UsedSpace { get; set; }
    public long TotalSpace { get; set; }
    public bool IsConnected { get; set; }
    public DateTime LastConnected { get; set; }
}