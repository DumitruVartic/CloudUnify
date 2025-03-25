namespace CloudUnify.Core.Models;

public class ProviderInfo {
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public bool IsConnected { get; set; }
    public string? UserId { get; set; }
    public DateTime? LastConnected { get; set; }
    public string? ClientSecretsPath { get; set; }
}