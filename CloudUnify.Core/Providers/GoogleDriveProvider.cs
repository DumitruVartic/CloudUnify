using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

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