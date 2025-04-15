using CloudUnify.Core;

namespace CloudUnify.Maui.Services;

public class CloudFileSystemService {
    private readonly CloudUnifyManager _cloudUnifyManager;

    public CloudFileSystemService(CloudUnifyManager cloudUnifyManager) {
        _cloudUnifyManager = cloudUnifyManager;
    }

    public async Task<UnifiedCloudFile> CreateFolderAsync(string name, string path, string providerId) {
        // Create a dummy file with 0 bytes to represent a folder
        var content = Array.Empty<byte>();
        var folderPath = Path.Combine(path, name).Replace("\\", "/");

        var folder = await _cloudUnifyManager.UploadFileAsync(content, name, folderPath, providerId);
        return folder;
    }

    public async Task<UnifiedCloudFile> RenameAsync(string fileId, string newName, string providerId) {
        return await _cloudUnifyManager.RenameFileAsync(fileId, newName, providerId);
    }

    public async Task DeleteAsync(string fileId, string providerId) {
        await _cloudUnifyManager.DeleteFileAsync(fileId, providerId);
    }

    public async Task<UnifiedCloudFile> MoveAsync(string fileId, string newPath, string providerId) {
        return await _cloudUnifyManager.MoveFileAsync(fileId, newPath, providerId);
    }

    public async Task<UnifiedCloudFile> CopyAsync(string fileId, string newPath, string providerId) {
        return await _cloudUnifyManager.CopyFileAsync(fileId, newPath, providerId);
    }

    public async Task<byte[]> DownloadAsync(string fileId, string providerId) {
        return await _cloudUnifyManager.DownloadFileAsync(fileId, providerId);
    }

    public async Task<UnifiedCloudFile> UploadAsync(byte[] content, string fileName, string path, string providerId) {
        return await _cloudUnifyManager.UploadFileAsync(content, fileName, path, providerId);
    }
}