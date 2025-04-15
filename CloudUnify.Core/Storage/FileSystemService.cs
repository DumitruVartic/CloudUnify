using System.Collections.Concurrent;
using System.Diagnostics;

namespace CloudUnify.Core.Storage;

public class FileSystemService {
    private readonly CloudUnify _cloudUnify;
    private readonly ConcurrentDictionary<string, UnifiedCloudFile> _fileCache;

    public FileSystemService(CloudUnify cloudUnify) {
        _cloudUnify = cloudUnify;
        _fileCache = new ConcurrentDictionary<string, UnifiedCloudFile>();
    }

    public async Task<List<UnifiedCloudFile>> ListFilesAsync(string path = "/", string? providerId = null) {
        try {
            Debug.WriteLine($"Listing files for path: {path}, providerId: {providerId}");

            if (providerId != null) {
                var files = await _cloudUnify.ListAllFilesAsync(path);
                var filteredFiles = files.Where(f => f.ProviderId == providerId).ToList();
                Debug.WriteLine($"Found {filteredFiles.Count} files for provider {providerId}");
                return filteredFiles;
            }

            var allFiles = await _cloudUnify.ListAllFilesAsync(path);
            Debug.WriteLine($"Found {allFiles.Count} files in total");
            foreach (var file in allFiles) _fileCache[GetCacheKey(file.Id, file.ProviderId)] = file;
            return allFiles;
        }
        catch (ArgumentException ex) {
            Debug.WriteLine($"Error listing files: {ex.Message}");
            return new List<UnifiedCloudFile>();
        }
        catch (Exception ex) {
            Debug.WriteLine($"Unexpected error listing files: {ex}");
            return new List<UnifiedCloudFile>();
        }
    }

    public async Task<UnifiedCloudFile?> GetFileAsync(string fileId, string providerId) {
        var cacheKey = GetCacheKey(fileId, providerId);
        if (_fileCache.TryGetValue(cacheKey, out var cachedFile)) return cachedFile;

        var file = await _cloudUnify.GetFileAsync(fileId, providerId);
        if (file != null) _fileCache[cacheKey] = file;
        return file;
    }

    public async Task<byte[]> DownloadFileAsync(string fileId, string providerId) {
        return await _cloudUnify.DownloadFileAsync(fileId, providerId);
    }

    public async Task<UnifiedCloudFile> CreateFolderAsync(string name, string path, string providerId) {
        // Create an empty byte array as content for the folder
        var folderContent = Array.Empty<byte>();
        var folder = await _cloudUnify.UploadFileAsync(folderContent, name, path, providerId);
        _fileCache[GetCacheKey(folder.Id, providerId)] = folder;
        return folder;
    }

    public async Task<UnifiedCloudFile>
        UploadFileAsync(byte[] content, string fileName, string path, string providerId) {
        var file = await _cloudUnify.UploadFileAsync(content, fileName, path, providerId);
        _fileCache[GetCacheKey(file.Id, providerId)] = file;
        return file;
    }

    public async Task DeleteAsync(string fileId, string providerId) {
        await _cloudUnify.DeleteFileAsync(fileId, providerId);
        var cacheKey = GetCacheKey(fileId, providerId);
        _fileCache.TryRemove(cacheKey, out _);
    }

    public async Task<UnifiedCloudFile> MoveAsync(string fileId, string newPath, string providerId) {
        var file = await _cloudUnify.MoveFileAsync(fileId, newPath, providerId);
        _fileCache[GetCacheKey(file.Id, providerId)] = file;
        return file;
    }

    public async Task<UnifiedCloudFile> CopyAsync(string fileId, string newPath, string providerId) {
        var file = await _cloudUnify.CopyFileAsync(fileId, newPath, providerId);
        _fileCache[GetCacheKey(file.Id, providerId)] = file;
        return file;
    }

    public async Task<UnifiedCloudFile> RenameAsync(string fileId, string newName, string providerId) {
        var file = await _cloudUnify.RenameFileAsync(fileId, newName, providerId);
        _fileCache[GetCacheKey(file.Id, providerId)] = file;
        return file;
    }

    public async Task<UnifiedCloudFile> CopyBetweenProvidersAsync(
        string sourceFileId,
        string sourceProviderId,
        string destinationPath,
        string destinationProviderId) {
        var file = await _cloudUnify.CopyFileBetweenProvidersAsync(
            sourceFileId,
            sourceProviderId,
            destinationPath,
            destinationProviderId);

        _fileCache[GetCacheKey(file.Id, destinationProviderId)] = file;
        return file;
    }

    private static string GetCacheKey(string fileId, string providerId) {
        return $"{providerId}:{fileId}";
    }
}