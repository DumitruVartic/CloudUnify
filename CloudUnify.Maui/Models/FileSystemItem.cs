namespace CloudUnify.Maui.Models;

public class FileSystemItem {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsFolder { get; set; }
    public bool IsDirectory => IsFolder; // For backward compatibility
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public DateTime LastModified => ModifiedAt; // For backward compatibility
    public string Provider { get; set; }
    public string MimeType { get; set; }
    public bool IsSelected { get; set; }
    public string Extension => IsFolder ? null : System.IO.Path.GetExtension(Name)?.ToLowerInvariant();
    public string ProviderIconClass => GetProviderIconClass();
    public string ProviderColor => GetProviderColor();
    public string Icon => GetFileIcon();

    private string GetFileIcon() {
        if (IsFolder) return "folder-fill";

        return Extension switch {
            ".pdf" => "file-pdf",
            ".doc" or ".docx" => "file-word",
            ".xls" or ".xlsx" => "file-excel",
            ".ppt" or ".pptx" => "file-ppt",
            ".txt" => "file-text",
            ".jpg" or ".jpeg" or ".png" or ".gif" => "file-image",
            ".mp3" or ".wav" or ".ogg" => "file-music",
            ".mp4" or ".avi" or ".mkv" => "file-play",
            ".zip" or ".rar" or ".7z" => "file-zip",
            _ => "file"
        };
    }

    private string GetProviderIconClass() {
        return Provider?.ToLowerInvariant() switch {
            "onedrive" => "microsoft",
            "googledrive" => "google",
            "dropbox" => "dropbox",
            _ => "cloud"
        };
    }

    private string GetProviderColor() {
        return Provider?.ToLowerInvariant() switch {
            "onedrive" => "#0078D4",
            "googledrive" => "#4285F4",
            "dropbox" => "#0061FF",
            _ => "#6c757d"
        };
    }
}