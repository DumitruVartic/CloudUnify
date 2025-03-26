namespace CloudUnify.Maui.Services;

public class SecureStorageService {
    private const string GoogleDriveSecretsKey = "client_secret_google_drive";
    private const string OneDriveSecretsKey = "client_secret_onedrive";

    public async Task SaveClientSecretsAsync(string googleDriveSecrets, string oneDriveSecrets) {
        await SecureStorage.Default.SetAsync(GoogleDriveSecretsKey, googleDriveSecrets);
        await SecureStorage.Default.SetAsync(OneDriveSecretsKey, oneDriveSecrets);
    }

    public async Task<string?> GetGoogleDriveSecretsAsync() {
        return await SecureStorage.Default.GetAsync(GoogleDriveSecretsKey);
    }

    public async Task<string?> GetOneDriveSecretsAsync() {
        return await SecureStorage.Default.GetAsync(OneDriveSecretsKey);
    }

    public async Task<bool> HasClientSecretsAsync() {
        var googleSecrets = await GetGoogleDriveSecretsAsync();
        var oneDriveSecrets = await GetOneDriveSecretsAsync();
        return !string.IsNullOrEmpty(googleSecrets) && !string.IsNullOrEmpty(oneDriveSecrets);
    }
}