using System.Collections.Concurrent;
using System.Diagnostics;

namespace CloudUnify.Core.Storage;

public class FileSystemService {
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5); // Cache for 5 minutes
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly CloudUnify _cloudUnify;
    private readonly ConcurrentDictionary<string, UnifiedCloudFile> _fileCache;
    private readonly ConcurrentDictionary<string, (List<UnifiedCloudFile> Files, DateTime CachedAt)> _folderCache;
    private readonly HashSet<string> _preloadPaths = new() { "/", "/Documents", "/Photos", "/Videos" };
    private bool _isPreloading;

    public FileSystemService(CloudUnify cloudUnify) {
        _cloudUnify = cloudUnify;
        _fileCache = new ConcurrentDictionary<string, UnifiedCloudFile>();
        _folderCache = new ConcurrentDictionary<string, (List<UnifiedCloudFile>, DateTime)>();

        // Start preloading common paths
        Task.Run(PreloadCommonPathsAsync);
    }

    private async Task PreloadCommonPathsAsync() {
        if (_isPreloading) return;

        try {
            _isPreloading = true;
            foreach (var path in _preloadPaths) {
                await ListFilesAsync(path);
                await Task.Delay(1000); // Delay between preloads to avoid overwhelming the system
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error preloading paths: {ex.Message}");
        }
        finally {
            _isPreloading = false;
        }
    }

    public async Task<List<UnifiedCloudFile>> ListFilesAsync(string path = "/", string? providerId = null) {
        try {
            Debug.WriteLine($"Listing files for path: {path}, providerId: {providerId}");

            // Try to get from cache first
            var cacheKey = GetFolderCacheKey(path, providerId);
            if (_folderCache.TryGetValue(cacheKey, out var cachedData)) {
                if (DateTime.UtcNow - cachedData.CachedAt <= _cacheDuration) {
                    Debug.WriteLine($"Returning {cachedData.Files.Count} files from cache for {cacheKey}");
                    return new List<UnifiedCloudFile>(cachedData.Files); // Return a copy to prevent modification
                }

                // Cache expired, remove it
                _folderCache.TryRemove(cacheKey, out _);
            }

            // Ensure only one thread is updating the cache for this path at a time
            await _cacheLock.WaitAsync();
            try {
                // Double-check if another thread has already updated the cache
                if (_folderCache.TryGetValue(cacheKey, out cachedData) &&
                    DateTime.UtcNow - cachedData.CachedAt <= _cacheDuration)
                    return new List<UnifiedCloudFile>(cachedData.Files);

                List<UnifiedCloudFile> files;
                if (providerId != null) {
                    files = await _cloudUnify.ListAllFilesAsync(path);
                    files = files.Where(f => f.ProviderId == providerId).ToList();
                    Debug.WriteLine($"Found {files.Count} files for provider {providerId}");
                }
                else {
                    // Load files from all providers in parallel
                    var providers = _cloudUnify.GetProviders();
                    var tasks = providers.Select(async provider => {
                        try {
                            if (!provider.IsConnected) return new List<UnifiedCloudFile>();
                            return await provider.ListFilesAsync(path);
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Error loading files from provider {provider.Name}: {ex.Message}");
                            return new List<UnifiedCloudFile>();
                        }
                    });

                    var results = await Task.WhenAll(tasks);
                    files = results.SelectMany(f => f).ToList();
                    Debug.WriteLine($"Found {files.Count} files in total from parallel load");
                }

                // Update both caches
                _folderCache[cacheKey] = (files, DateTime.UtcNow);
                foreach (var file in files) {
                    var fileCacheKey = GetFileCacheKey(file.Id, file.ProviderId);
                    _fileCache[fileCacheKey] = file;
                }

                // Start preloading subfolders for better navigation experience
                if (!_isPreloading)
                    _ = Task.Run(async () => {
                        foreach (var folder in files.Where(f => f.IsFolder).Take(5)) {
                            await ListFilesAsync(folder.Path, folder.ProviderId);
                            await Task.Delay(500); // Small delay between subfolder loads
                        }
                    });

                return new List<UnifiedCloudFile>(files);
            }
            finally {
                _cacheLock.Release();
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error in ListFilesAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<UnifiedCloudFile?> GetFileAsync(string fileId, string providerId) {
        var cacheKey = GetFileCacheKey(fileId, providerId);

        // Try to get from cache first
        if (_fileCache.TryGetValue(cacheKey, out var cachedFile)) {
            Debug.WriteLine($"Returning file {fileId} from cache");
            return cachedFile;
        }

        try {
            var file = await _cloudUnify.GetFileAsync(fileId, providerId);
            if (file != null) _fileCache[cacheKey] = file;
            return file;
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error getting file {fileId}: {ex.Message}");
            return null;
        }
    }

    public async Task<byte[]> DownloadFileAsync(string fileId, string providerId) {
        return await _cloudUnify.DownloadFileAsync(fileId, providerId);
    }

    public async Task<UnifiedCloudFile> CreateFolderAsync(string name, string path, string providerId) {
        // Create an empty byte array as content for the folder
        var folderContent = Array.Empty<byte>();
        var folder = await _cloudUnify.UploadFileAsync(folderContent, name, path, providerId);
        _fileCache[GetFileCacheKey(folder.Id, providerId)] = folder;
        return folder;
    }

    public async Task<UnifiedCloudFile>
        UploadFileAsync(byte[] content, string fileName, string path, string providerId) {
        var file = await _cloudUnify.UploadFileAsync(content, fileName, path, providerId);
        _fileCache[GetFileCacheKey(file.Id, providerId)] = file;
        return file;
    }

    public async Task DeleteAsync(string fileId, string providerId) {
        await _cloudUnify.DeleteFileAsync(fileId, providerId);
        var cacheKey = GetFileCacheKey(fileId, providerId);
        _fileCache.TryRemove(cacheKey, out _);
    }

    public async Task<UnifiedCloudFile> MoveAsync(string fileId, string newPath, string providerId) {
        var file = await _cloudUnify.MoveFileAsync(fileId, newPath, providerId);
        _fileCache[GetFileCacheKey(file.Id, providerId)] = file;
        return file;
    }

    public async Task<UnifiedCloudFile> CopyAsync(string fileId, string newPath, string providerId) {
        var file = await _cloudUnify.CopyFileAsync(fileId, newPath, providerId);
        _fileCache[GetFileCacheKey(file.Id, providerId)] = file;
        return file;
    }

    public async Task<UnifiedCloudFile> RenameAsync(string fileId, string newName, string providerId) {
        var file = await _cloudUnify.RenameFileAsync(fileId, newName, providerId);
        _fileCache[GetFileCacheKey(file.Id, providerId)] = file;
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

        _fileCache[GetFileCacheKey(file.Id, destinationProviderId)] = file;
        return file;
    }

    public void InvalidateCache(string? path = null, string? providerId = null) {
        if (path == null && providerId == null) {
            _folderCache.Clear();
            _fileCache.Clear();
            return;
        }

        var keysToRemove = _folderCache.Keys
            .Where(k => (path == null || k.StartsWith($"{path}:")) &&
                        (providerId == null || k.EndsWith($":{providerId}")))
            .ToList();

        foreach (var key in keysToRemove) _folderCache.TryRemove(key, out _);

        var fileKeysToRemove = _fileCache.Keys
            .Where(k => providerId == null || k.EndsWith($":{providerId}"))
            .ToList();

        foreach (var key in fileKeysToRemove) _fileCache.TryRemove(key, out _);
    }

    private string GetFolderCacheKey(string path, string? providerId) {
        return $"{path}:{providerId ?? "all"}";
    }

    private string GetFileCacheKey(string fileId, string providerId) {
        return $"{fileId}:{providerId}";
    }
}