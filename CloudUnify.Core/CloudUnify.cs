namespace CloudUnify.Core;

public class CloudUnify {
    private readonly List<ICloudProvider> _cloudProviders = new();

    public void RegisterProvider(ICloudProvider provider) {
        _cloudProviders.Add(provider);
    }

    public async Task<List<UnifiedCloudFile>> ListAllFilesAsync(string path = "/") {
        var allFiles = new List<UnifiedCloudFile>();

        foreach (var provider in _cloudProviders)
            try {
                var files = await provider.ListFilesAsync(path);
                allFiles.AddRange(files);
            }
            catch (Exception ex) {
                // Log the error but continue with other providers
                Console.WriteLine($"Error listing files from {provider.Name}: {ex.Message}");
            }

        return allFiles;
    }

    public async Task<UnifiedCloudFile?> GetFileAsync(string fileId, string providerId) {
        var provider = _cloudProviders.Find(p => p.Id == providerId);
        if (provider == null) throw new ArgumentException($"Provider with ID {providerId} not found");

        return await provider.GetFileAsync(fileId);
    }

    public async Task<byte[]> DownloadFileAsync(string fileId, string providerId) {
        var provider = _cloudProviders.Find(p => p.Id == providerId);
        if (provider == null) throw new ArgumentException($"Provider with ID {providerId} not found");

        return await provider.DownloadFileAsync(fileId);
    }

    public async Task<UnifiedCloudFile>
        UploadFileAsync(byte[] content, string fileName, string path, string providerId) {
        var provider = _cloudProviders.Find(p => p.Id == providerId);
        if (provider == null) throw new ArgumentException($"Provider with ID {providerId} not found");

        return await provider.UploadFileAsync(content, fileName, path);
    }

    public async Task DeleteFileAsync(string fileId, string providerId) {
        var provider = _cloudProviders.Find(p => p.Id == providerId);
        if (provider == null) throw new ArgumentException($"Provider with ID {providerId} not found");

        await provider.DeleteFileAsync(fileId);
    }

    public async Task<UnifiedCloudFile> MoveFileAsync(string fileId, string newPath, string providerId) {
        var provider = _cloudProviders.Find(p => p.Id == providerId);
        if (provider == null) throw new ArgumentException($"Provider with ID {providerId} not found");

        return await provider.MoveFileAsync(fileId, newPath);
    }

    public async Task<UnifiedCloudFile> CopyFileAsync(string fileId, string newPath, string providerId) {
        var provider = _cloudProviders.Find(p => p.Id == providerId);
        if (provider == null) throw new ArgumentException($"Provider with ID {providerId} not found");

        return await provider.CopyFileAsync(fileId, newPath);
    }

    public async Task<UnifiedCloudFile> RenameFileAsync(string fileId, string newName, string providerId) {
        var provider = _cloudProviders.Find(p => p.Id == providerId);
        if (provider == null) throw new ArgumentException($"Provider with ID {providerId} not found");

        return await provider.RenameFileAsync(fileId, newName);
    }

    public async Task<List<CloudStorageInfo>> GetStorageInfoAsync() {
        var storageInfoList = new List<CloudStorageInfo>();

        foreach (var provider in _cloudProviders)
            try {
                var storageInfo = await provider.GetStorageInfoAsync();
                storageInfoList.Add(storageInfo);
            }
            catch (Exception ex) {
                // Log the error but continue with other providers
                Console.WriteLine($"Error getting storage info from {provider.Name}: {ex.Message}");
            }

        return storageInfoList;
    }

    public async Task<UnifiedCloudFile> CopyFileBetweenProvidersAsync(
        string sourceFileId,
        string sourceProviderId,
        string destinationPath,
        string destinationProviderId) {
        var sourceProvider = _cloudProviders.Find(p => p.Id == sourceProviderId);
        var destinationProvider = _cloudProviders.Find(p => p.Id == destinationProviderId);

        if (sourceProvider == null)
            throw new ArgumentException($"Source provider with ID {sourceProviderId} not found");

        if (destinationProvider == null)
            throw new ArgumentException($"Destination provider with ID {destinationProviderId} not found");

        // Download from source
        var fileContent = await sourceProvider.DownloadFileAsync(sourceFileId);
        var sourceFile = await sourceProvider.GetFileAsync(sourceFileId);

        if (sourceFile == null) throw new InvalidOperationException($"Source file with ID {sourceFileId} not found");

        // Upload to destination
        return await destinationProvider.UploadFileAsync(fileContent, sourceFile.Name, destinationPath);
    }
}