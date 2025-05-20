using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CloudUnify.Core.Models;

namespace CloudUnify.Core.Providers;

public class OneDriveProvider : ICloudProvider {
    private const string ApiBaseUrl = "https://graph.microsoft.com/v1.0";
    private readonly string _applicationName;
    private readonly HttpClient _httpClient;
    private string _accessToken = string.Empty;
    private string _userEmail = string.Empty;

    public OneDriveProvider(string id, string applicationName) {
        Id = id;
        _applicationName = applicationName;
        _httpClient = new HttpClient();
    }

    public string Id { get; }
    public string Name => "OneDrive";
    public bool IsConnected => !string.IsNullOrEmpty(_accessToken);

    public async Task<bool> ConnectAsync(string accessToken) {
        try {
            _accessToken = accessToken;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            // Get user info to identify this account
            var userResponse = await _httpClient.GetAsync($"{ApiBaseUrl}/me");
            userResponse.EnsureSuccessStatusCode();

            var userJson = await userResponse.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<JsonElement>(userJson);

            if (userInfo.TryGetProperty("userPrincipalName", out var email))
                _userEmail = email.GetString() ?? string.Empty;
            else if (userInfo.TryGetProperty("mail", out email)) _userEmail = email.GetString() ?? string.Empty;

            return true;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error connecting to OneDrive: {ex.Message}");
            _accessToken = string.Empty;
            return false;
        }
    }

    public Task DisconnectAsync() {
        _accessToken = string.Empty;
        _userEmail = string.Empty;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        return Task.CompletedTask;
    }

    public async Task<List<UnifiedCloudFile>> ListFilesAsync(string path = "/") {
        if (!IsConnected) throw new InvalidOperationException("Not connected to OneDrive");

        var files = new List<UnifiedCloudFile>();
        string driveItemPath;

        if (path == "/" || path == "root") {
            driveItemPath = "/drive/root/children";
        }
        else {
            // Remove leading slash and encode path
            var encodedPath = path.TrimStart('/').Replace("/", ":");
            driveItemPath = $"/drive/root:/{encodedPath}:/children";
        }

        try {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}{driveItemPath}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);

            if (jsonResponse.TryGetProperty("value", out var items))
                foreach (var item in items.EnumerateArray())
                    files.Add(MapToUnifiedCloudFile(item, path));

            return files;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error listing OneDrive files: {ex.Message}");
            throw;
        }
    }

    public async Task<UnifiedCloudFile?> GetFileAsync(string fileId) {
        if (!IsConnected) throw new InvalidOperationException("Not connected to OneDrive");

        try {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/drive/items/{fileId}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var item = JsonSerializer.Deserialize<JsonElement>(content);

            // Get the parent path
            var path = await GetPathFromFileIdAsync(fileId);

            return MapToUnifiedCloudFile(item, path);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error getting OneDrive file: {ex.Message}");
            return null;
        }
    }

    public async Task<byte[]> DownloadFileAsync(string fileId) {
        if (!IsConnected) throw new InvalidOperationException("Not connected to OneDrive");

        try {
            // First get the download URL
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/drive/items/{fileId}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var item = JsonSerializer.Deserialize<JsonElement>(content);

            if (item.TryGetProperty("@microsoft.graph.downloadUrl", out var downloadUrl)) {
                // Use the download URL to get the file content
                var fileResponse = await _httpClient.GetAsync(downloadUrl.GetString());
                fileResponse.EnsureSuccessStatusCode();

                return await fileResponse.Content.ReadAsByteArrayAsync();
            }

            throw new InvalidOperationException("Download URL not found for file");
        }
        catch (Exception ex) {
            Console.WriteLine($"Error downloading OneDrive file: {ex.Message}");
            throw;
        }
    }

    public async Task<UnifiedCloudFile> UploadFileAsync(byte[] content, string fileName, string path) {
        if (!IsConnected) throw new InvalidOperationException("Not connected to OneDrive");

        try {
            string uploadUrl;

            if (path == "/" || path == "root") {
                uploadUrl = $"{ApiBaseUrl}/drive/root:/{Uri.EscapeDataString(fileName)}:/content";
            }
            else {
                var encodedPath = path.TrimStart('/');
                uploadUrl = $"{ApiBaseUrl}/drive/root:/{encodedPath}/{Uri.EscapeDataString(fileName)}:/content";
            }

            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));

            var response = await _httpClient.PutAsync(uploadUrl, fileContent);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var item = JsonSerializer.Deserialize<JsonElement>(responseContent);

            return MapToUnifiedCloudFile(item, path);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error uploading to OneDrive: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteFileAsync(string fileId) {
        if (!IsConnected) throw new InvalidOperationException("Not connected to OneDrive");

        try {
            var response = await _httpClient.DeleteAsync($"{ApiBaseUrl}/drive/items/{fileId}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error deleting OneDrive file: {ex.Message}");
            throw;
        }
    }

    public async Task<UnifiedCloudFile> MoveFileAsync(string fileId, string newPath) {
        if (!IsConnected) throw new InvalidOperationException("Not connected to OneDrive");

        try {
            // Get the parent folder ID for the new path
            string parentId;
            if (newPath == "/" || newPath == "root") {
                parentId = "root";
            }
            else {
                var encodedPath = newPath.TrimStart('/');
                var parentResponse = await _httpClient.GetAsync($"{ApiBaseUrl}/drive/root:/{encodedPath}");
                parentResponse.EnsureSuccessStatusCode();

                var parentContent = await parentResponse.Content.ReadAsStringAsync();
                var parentItem = JsonSerializer.Deserialize<JsonElement>(parentContent);

                if (!parentItem.TryGetProperty("id", out var id))
                    throw new InvalidOperationException("Could not get parent folder ID");

                parentId = id.GetString();
            }

            // Create the move request
            var moveRequest = new {
                parentReference = new {
                    id = parentId
                }
            };

            var json = JsonSerializer.Serialize(moveRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync($"{ApiBaseUrl}/drive/items/{fileId}", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var item = JsonSerializer.Deserialize<JsonElement>(responseContent);

            return MapToUnifiedCloudFile(item, newPath);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error moving OneDrive file: {ex.Message}");
            throw;
        }
    }

    public async Task<UnifiedCloudFile> CopyFileAsync(string fileId, string newPath) {
        if (!IsConnected) throw new InvalidOperationException("Not connected to OneDrive");

        try {
            // Get file info to get the name
            var fileResponse = await _httpClient.GetAsync($"{ApiBaseUrl}/drive/items/{fileId}");
            fileResponse.EnsureSuccessStatusCode();

            var fileContent = await fileResponse.Content.ReadAsStringAsync();
            var fileItem = JsonSerializer.Deserialize<JsonElement>(fileContent);

            if (!fileItem.TryGetProperty("name", out var fileName))
                throw new InvalidOperationException("Could not get file name");

            // Get the parent folder ID for the new path
            string parentId;
            if (newPath == "/" || newPath == "root") {
                parentId = "root";
            }
            else {
                var encodedPath = newPath.TrimStart('/');
                var parentResponse = await _httpClient.GetAsync($"{ApiBaseUrl}/drive/root:/{encodedPath}");
                parentResponse.EnsureSuccessStatusCode();

                var parentContent = await parentResponse.Content.ReadAsStringAsync();
                var parentItem = JsonSerializer.Deserialize<JsonElement>(parentContent);

                if (!parentItem.TryGetProperty("id", out var id))
                    throw new InvalidOperationException("Could not get parent folder ID");

                parentId = id.GetString();
            }

            // Create the copy request
            var copyRequest = new {
                parentReference = new {
                    id = parentId
                },
                name = fileName.GetString()
            };

            var json = JsonSerializer.Serialize(copyRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // OneDrive requires a specific endpoint for copy operations
            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/drive/items/{fileId}/copy", content);

            // Copy is asynchronous, so we need to monitor the operation
            if (response.StatusCode == HttpStatusCode.Accepted) {
                // Get the location header to check the status
                var monitorUrl = response.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(monitorUrl))
                    throw new InvalidOperationException("Copy operation started but no monitor URL was provided");

                // Wait for the copy to complete (with timeout)
                var timeout = DateTime.Now.AddMinutes(5);
                while (DateTime.Now < timeout) {
                    await Task.Delay(1000); // Wait 1 second between checks

                    var statusResponse = await _httpClient.GetAsync(monitorUrl);
                    if (statusResponse.StatusCode == HttpStatusCode.OK) {
                        // Copy completed, get the new file
                        var files = await ListFilesAsync(newPath);
                        var newFile = files.FirstOrDefault(f => f.Name == fileName.GetString());

                        if (newFile != null) return newFile;

                        throw new InvalidOperationException("Copy completed but file not found");
                    }

                    if (statusResponse.StatusCode != HttpStatusCode.Accepted)
                        // Error occurred
                        throw new InvalidOperationException($"Copy failed with status: {statusResponse.StatusCode}");

                    // Still in progress, continue waiting
                }

                throw new TimeoutException("Copy operation timed out");
            }

            throw new InvalidOperationException($"Copy failed with status: {response.StatusCode}");
        }
        catch (Exception ex) {
            Console.WriteLine($"Error copying OneDrive file: {ex.Message}");
            throw;
        }
    }

    public async Task<UnifiedCloudFile> RenameFileAsync(string fileId, string newName) {
        if (!IsConnected) throw new InvalidOperationException("Not connected to OneDrive");

        try {
            // Create the rename request
            var renameRequest = new {
                name = newName
            };

            var json = JsonSerializer.Serialize(renameRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync($"{ApiBaseUrl}/drive/items/{fileId}", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var item = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Get the path
            var path = await GetPathFromFileIdAsync(fileId);

            return MapToUnifiedCloudFile(item, path);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error renaming OneDrive file: {ex.Message}");
            throw;
        }
    }

    public async Task<CloudStorageInfo> GetStorageInfoAsync() {
        try {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/me/drive");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var driveInfo = JsonSerializer.Deserialize<JsonElement>(content);

            var totalSpace = driveInfo.GetProperty("quota").GetProperty("total").GetInt64();
            var usedSpace = driveInfo.GetProperty("quota").GetProperty("used").GetInt64();

            return new CloudStorageInfo {
                ProviderId = Id,
                ProviderName = Name,
                UserEmail = _userEmail,
                TotalSpace = totalSpace,
                UsedSpace = usedSpace
            };
        }
        catch (Exception ex) {
            Console.WriteLine($"Error getting OneDrive storage info: {ex.Message}");
            throw;
        }
    }

    public async Task<AccountInfo?> GetAccountInfoAsync() {
        try {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/me");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<JsonElement>(content);

            string? displayName = null;
            string? email = null;

            if (userInfo.TryGetProperty("displayName", out var nameElement)) {
                displayName = nameElement.GetString();
            }

            if (userInfo.TryGetProperty("userPrincipalName", out var emailElement)) {
                email = emailElement.GetString();
            }
            else if (userInfo.TryGetProperty("mail", out emailElement)) {
                email = emailElement.GetString();
            }

            return new AccountInfo {
                DisplayName = displayName ?? email ?? "OneDrive User",
                Email = email ?? _userEmail
            };
        }
        catch (Exception ex) {
            Console.WriteLine($"Error getting OneDrive account info: {ex.Message}");
            return null;
        }
    }

    private async Task<string> GetPathFromFileIdAsync(string fileId) {
        try {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/drive/items/{fileId}/path");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var pathInfo = JsonSerializer.Deserialize<JsonElement>(content);

            if (pathInfo.TryGetProperty("path", out var pathValue)) {
                var path = pathValue.GetString() ?? "/";

                // The path from API includes /drive/root: prefix, remove it
                if (path.StartsWith("/drive/root:")) path = path.Substring("/drive/root:".Length);

                // If empty, return root
                return string.IsNullOrEmpty(path) ? "/" : path;
            }

            return "/";
        }
        catch {
            return "/";
        }
    }

    private UnifiedCloudFile MapToUnifiedCloudFile(JsonElement item, string path) {
        var file = new UnifiedCloudFile {
            Id = item.GetProperty("id").GetString() ?? string.Empty,
            Name = item.GetProperty("name").GetString() ?? string.Empty,
            Path = path,
            ProviderId = Id,
            ProviderName = Name
        };

        // Check if it's a folder
        if (item.TryGetProperty("folder", out _)) {
            file.IsFolder = true;
            file.MimeType = "folder";
        }
        else {
            file.IsFolder = false;

            if (item.TryGetProperty("file", out var fileInfo) &&
                fileInfo.TryGetProperty("mimeType", out var mimeType))
                file.MimeType = mimeType.GetString() ?? string.Empty;

            if (item.TryGetProperty("size", out var size)) file.Size = size.GetInt64();
        }

        if (item.TryGetProperty("createdDateTime", out var created)) file.CreatedAt = created.GetDateTime();

        if (item.TryGetProperty("lastModifiedDateTime", out var modified)) file.ModifiedAt = modified.GetDateTime();

        if (item.TryGetProperty("webUrl", out var webUrl)) file.WebViewLink = webUrl.GetString() ?? string.Empty;

        if (item.TryGetProperty("thumbnails", out var thumbnails) &&
            thumbnails.EnumerateArray().Any() &&
            thumbnails[0].TryGetProperty("medium", out var medium) &&
            medium.TryGetProperty("url", out var url))
            file.ThumbnailUrl = url.GetString() ?? string.Empty;

        return file;
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