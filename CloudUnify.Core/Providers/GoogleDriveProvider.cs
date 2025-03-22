using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using File = Google.Apis.Drive.v3.Data.File;

namespace CloudUnify.Core.Providers;

public class GoogleDriveProvider : ICloudProvider {
    private readonly string _applicationName;
    private DriveService? _driveService;
    private string _userEmail = string.Empty;

    public GoogleDriveProvider(string id, string applicationName) {
        Id = id;
        _applicationName = applicationName;
    }

    public string Id { get; }
    public string Name => "Google Drive";
    public bool IsConnected => _driveService != null;

    public async Task<bool> ConnectAsync(string accessToken) {
        try {
            var credential = GoogleCredential.FromAccessToken(accessToken)
                .CreateScoped(DriveService.Scope.Drive);

            _driveService = new DriveService(new BaseClientService.Initializer {
                HttpClientInitializer = credential,
                ApplicationName = _applicationName
            });

            // Get user info to identify this account
            var aboutRequest = _driveService.About.Get();
            aboutRequest.Fields = "user";
            var about = await aboutRequest.ExecuteAsync();
            _userEmail = about.User.EmailAddress;

            return true;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error connecting to Google Drive: {ex.Message}");
            return false;
        }
    }

    public Task DisconnectAsync() {
        _driveService = null;
        _userEmail = string.Empty;
        return Task.CompletedTask;
    }

    public async Task<List<UnifiedCloudFile>> ListFilesAsync(string path = "/") {
        if (_driveService == null) throw new InvalidOperationException("Not connected to Google Drive");

        var files = new List<UnifiedCloudFile>();
        string? parentId = null;

        // If path is not root, find the folder ID
        if (path != "/" && path != "root") {
            var pathParts = path.Trim('/').Split('/');
            parentId = await GetFolderIdFromPathAsync(pathParts);

            if (parentId == null) throw new DirectoryNotFoundException($"Path not found: {path}");
        }
        else if (path == "root" || path == "/") {
            parentId = "root";
        }

        // List files in the folder
        var request = _driveService.Files.List();
        request.Q = parentId != null ? $"'{parentId}' in parents and trashed = false" : "trashed = false";
        request.Fields = "files(id, name, mimeType, size, createdTime, modifiedTime, webViewLink, thumbnailLink, parents)";
        request.PageSize = 1000;

        var result = await request.ExecuteAsync();

        foreach (var file in result.Files) files.Add(MapToUnifiedCloudFile(file, path));

        return files;
    }

    public async Task<UnifiedCloudFile?> GetFileAsync(string fileId) {
        if (_driveService == null) throw new InvalidOperationException("Not connected to Google Drive");

        var request = _driveService.Files.Get(fileId);
        request.Fields = "id, name, mimeType, size, createdTime, modifiedTime, webViewLink, thumbnailLink, parents";

        try {
            var file = await request.ExecuteAsync();
            var path = await GetPathFromFileIdAsync(fileId);
            return MapToUnifiedCloudFile(file, path);
        }
        catch (Exception) {
            return null;
        }
    }

    public async Task<byte[]> DownloadFileAsync(string fileId) {
        if (_driveService == null) throw new InvalidOperationException("Not connected to Google Drive");

        var request = _driveService.Files.Get(fileId);

        using var stream = new MemoryStream();
        await request.DownloadAsync(stream);
        return stream.ToArray();
    }

    public async Task<UnifiedCloudFile> UploadFileAsync(byte[] content, string fileName, string path) {
        if (_driveService == null) throw new InvalidOperationException("Not connected to Google Drive");

        // Find parent folder ID
        var parentId = "root";
        if (path != "/" && path != "root") {
            var pathParts = path.Trim('/').Split('/');
            parentId = await GetFolderIdFromPathAsync(pathParts);

            if (parentId == null) throw new DirectoryNotFoundException($"Path not found: {path}");
        }

        // Create file metadata
        var fileMetadata = new File {
            Name = fileName,
            Parents = new List<string> { parentId }
        };

        // Upload file
        using var stream = new MemoryStream(content);
        var request = _driveService.Files.Create(fileMetadata, stream, GetMimeType(fileName));
        request.Fields = "id, name, mimeType, size, createdTime, modifiedTime, webViewLink, thumbnailLink, parents";

        var file = await request.UploadAsync();
        var uploadedFile = request.ResponseBody;

        return MapToUnifiedCloudFile(uploadedFile, path);
    }

    public async Task DeleteFileAsync(string fileId) {
        if (_driveService == null) throw new InvalidOperationException("Not connected to Google Drive");

        await _driveService.Files.Delete(fileId).ExecuteAsync();
    }

    public async Task<UnifiedCloudFile> MoveFileAsync(string fileId, string newPath)
    {
        if (_driveService == null)
        {
            throw new InvalidOperationException("Not connected to Google Drive");
        }
        
        // Get current file to get its parents
        var getRequest = _driveService.Files.Get(fileId);
        getRequest.Fields = "parents";
        var file = await getRequest.ExecuteAsync();
        
        // Find new parent folder ID
        string? newParentId = "root";
        if (newPath != "/" && newPath != "root")
        {
            var pathParts = newPath.Trim('/').Split('/');
            newParentId = await GetFolderIdFromPathAsync(pathParts);
            
            if (newParentId == null)
            {
                throw new DirectoryNotFoundException($"Path not found: {newPath}");
            }
        }
        
        // Move file
        var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId);
        updateRequest.Fields = "id, name, mimeType, size, createdTime, modifiedTime, webViewLink, thumbnailLink, parents";
        
        // Remove old parents and add new parent
        updateRequest.RemoveParents = string.Join(",", file.Parents);
        updateRequest.AddParents = newParentId;
        
        var updatedFile = await updateRequest.ExecuteAsync();
        
        return MapToUnifiedCloudFile(updatedFile, newPath);
    }
    
    public async Task<UnifiedCloudFile> CopyFileAsync(string fileId, string newPath)
    {
        if (_driveService == null)
        {
            throw new InvalidOperationException("Not connected to Google Drive");
        }
        
        // Find new parent folder ID
        string? newParentId = "root";
        if (newPath != "/" && newPath != "root")
        {
            var pathParts = newPath.Trim('/').Split('/');
            newParentId = await GetFolderIdFromPathAsync(pathParts);
            
            if (newParentId == null)
            {
                throw new DirectoryNotFoundException($"Path not found: {newPath}");
            }
        }
        
        // Get file info
        var getRequest = _driveService.Files.Get(fileId);
        getRequest.Fields = "name";
        var sourceFile = await getRequest.ExecuteAsync();
        
        // Copy file
        var copyMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = sourceFile.Name,
            Parents = new List<string> { newParentId }
        };
        
        var copyRequest = _driveService.Files.Copy(copyMetadata, fileId);
        copyRequest.Fields = "id, name, mimeType, size, createdTime, modifiedTime, webViewLink, thumbnailLink, parents";
        
        var copiedFile = await copyRequest.ExecuteAsync();
        
        return MapToUnifiedCloudFile(copiedFile, newPath);
    }

    public Task<UnifiedCloudFile> RenameFileAsync(string fileId, string newName) {
        throw new NotImplementedException();
    }

    public Task<CloudStorageInfo> GetStorageInfoAsync() {
        throw new NotImplementedException();
    }

    // Internal use
    private async Task<string?> GetFolderIdFromPathAsync(string[] pathParts) {
        if (_driveService == null) throw new InvalidOperationException("Not connected to Google Drive");

        var currentFolderId = "root";

        foreach (var folderName in pathParts) {
            if (string.IsNullOrEmpty(folderName)) continue;

            var request = _driveService.Files.List();
            request.Q =
                $"name = '{folderName}' and '{currentFolderId}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id)";

            var result = await request.ExecuteAsync();

            if (result.Files.Count == 0) return null;

            currentFolderId = result.Files[0].Id;
        }

        return currentFolderId;
    }

    private async Task<string> GetPathFromFileIdAsync(string fileId) {
        if (_driveService == null) throw new InvalidOperationException("Not connected to Google Drive");

        var path = new List<string>();
        var currentId = fileId;
        var isRoot = false;

        while (!isRoot) {
            var request = _driveService.Files.Get(currentId);
            request.Fields = "name, parents";

            var file = await request.ExecuteAsync();

            if (file.Name != null) path.Add(file.Name);

            if (file.Parents == null || file.Parents.Count == 0 || file.Parents[0] == "root")
                isRoot = true;
            else
                currentId = file.Parents[0];
        }

        path.Reverse();
        return "/" + string.Join("/", path.Take(path.Count - 1)); // Exclude the file name itself
    }

    private UnifiedCloudFile MapToUnifiedCloudFile(File file, string path) {
        return new UnifiedCloudFile {
            Id = file.Id,
            Name = file.Name,
            Path = path,
            Size = file.Size ?? 0,
            CreatedAt = file.CreatedTimeDateTimeOffset?.DateTime ?? DateTime.MinValue,
            ModifiedAt = file.ModifiedTimeDateTimeOffset?.DateTime ?? DateTime.MinValue,
            MimeType = file.MimeType,
            IsFolder = file.MimeType == "application/vnd.google-apps.folder",
            ProviderId = Id,
            ProviderName = Name,
            WebViewLink = file.WebViewLink,
            ThumbnailUrl = file.ThumbnailLink
        };
    }

    private string GetMimeType(string fileName) {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };
    }
}