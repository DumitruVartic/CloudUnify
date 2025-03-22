namespace CloudUnify.Core;

public class UnifiedCloudFile {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string WebViewLink { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
}