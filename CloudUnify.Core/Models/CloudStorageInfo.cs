namespace CloudUnify.Core;

public class CloudStorageInfo {
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public long TotalSpace { get; set; }
    public long UsedSpace { get; set; }
    public long AvailableSpace => TotalSpace - UsedSpace;
    public double UsagePercentage => (double)UsedSpace / TotalSpace * 100;
}