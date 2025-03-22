namespace CloudUnify.Core;

public interface ICloudProvider {
    string Id { get; }
    string Name { get; }
    bool IsConnected { get; }

    Task<bool> ConnectAsync(string accessToken);
    Task DisconnectAsync();

    Task<List<UnifiedCloudFile>> ListFilesAsync(string path = "/");
    Task<UnifiedCloudFile?> GetFileAsync(string fileId);
    Task<byte[]> DownloadFileAsync(string fileId);
    Task<UnifiedCloudFile> UploadFileAsync(byte[] content, string fileName, string path);
    Task DeleteFileAsync(string fileId);
    Task<UnifiedCloudFile> MoveFileAsync(string fileId, string newPath);
    Task<UnifiedCloudFile> CopyFileAsync(string fileId, string newPath);
    Task<UnifiedCloudFile> RenameFileAsync(string fileId, string newName);
    Task<CloudStorageInfo> GetStorageInfoAsync();
}