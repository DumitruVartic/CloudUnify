namespace CloudUnify.Core.Providers;

public class OneDriveProvider : ICloudProvider {
    public string Id { get; }
    public string Name { get; }
    public bool IsConnected { get; }

    public Task<bool> ConnectAsync(string accessToken) {
        throw new NotImplementedException();
    }

    public Task DisconnectAsync() {
        throw new NotImplementedException();
    }

    public Task<List<UnifiedCloudFile>> ListFilesAsync(string path = "/") {
        throw new NotImplementedException();
    }

    public Task<UnifiedCloudFile?> GetFileAsync(string fileId) {
        throw new NotImplementedException();
    }

    public Task<byte[]> DownloadFileAsync(string fileId) {
        throw new NotImplementedException();
    }

    public Task<UnifiedCloudFile> UploadFileAsync(byte[] content, string fileName, string path) {
        throw new NotImplementedException();
    }

    public Task DeleteFileAsync(string fileId) {
        throw new NotImplementedException();
    }

    public Task<UnifiedCloudFile> MoveFileAsync(string fileId, string newPath) {
        throw new NotImplementedException();
    }

    public Task<UnifiedCloudFile> CopyFileAsync(string fileId, string newPath) {
        throw new NotImplementedException();
    }

    public Task<UnifiedCloudFile> RenameFileAsync(string fileId, string newName) {
        throw new NotImplementedException();
    }

    public Task<CloudStorageInfo> GetStorageInfoAsync() {
        throw new NotImplementedException();
    }
}