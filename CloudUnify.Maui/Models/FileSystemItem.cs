using System;

namespace CloudUnify.Maui.Models;

public class FileSystemItem
{
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string Extension => IsDirectory ? null : System.IO.Path.GetExtension(Name)?.ToLowerInvariant();
    public bool IsSelected { get; set; }
    
    public string Icon => GetFileIcon();
    
    private string GetFileIcon()
    {
        if (IsDirectory) return "folder-fill";
        
        return Extension switch
        {
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
} 