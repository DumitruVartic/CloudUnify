using System.Text.Json;

namespace CloudUnify.Core.Authentication;

public class OneDriveAuthProvider : IAuthProvider {
    private readonly string _applicationName;
    private readonly string _clientSecretsPath;
    private readonly string _dataStorePath;
    private readonly HttpClient _httpClient;

    public OneDriveAuthProvider(string clientSecretsPath, string applicationName, string dataStorePath) {
        _clientSecretsPath = clientSecretsPath;
        _applicationName = applicationName;
        _dataStorePath = dataStorePath;
        _httpClient = new HttpClient();
    }

    public async Task<string> AuthenticateAsync(string userId) {
        try {
            Console.WriteLine("Starting OneDrive authentication process...");

            // Load client secrets
            string clientId;
            string clientSecret;
            string redirectUri;

            using (var stream = new FileStream(_clientSecretsPath, FileMode.Open, FileAccess.Read)) {
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                var secrets = JsonSerializer.Deserialize<JsonElement>(json);

                if (secrets.TryGetProperty("web", out var web)) {
                    clientId = web.GetProperty("client_id").GetString();
                    clientSecret = web.GetProperty("client_secret").GetString();

                    // Get the first redirect URI
                    var redirectUris = web.GetProperty("redirect_uris").EnumerateArray();
                    redirectUri = redirectUris.First().GetString();
                }
                else if (secrets.TryGetProperty("installed", out var installed)) {
                    clientId = installed.GetProperty("client_id").GetString();
                    clientSecret = installed.GetProperty("client_secret").GetString();

                    // Get the first redirect URI
                    var redirectUris = installed.GetProperty("redirect_uris").EnumerateArray();
                    redirectUri = redirectUris.First().GetString();
                }
                else {
                    throw new InvalidOperationException("Invalid client secrets format");
                }
            }

            // Check if we have a stored token
            var tokenFilePath = Path.Combine(_dataStorePath, $"onedrive_token_{userId}.json");
            if (File.Exists(tokenFilePath)) {
                var _tokenJson = await File.ReadAllTextAsync(tokenFilePath);
                var _tokenData = JsonSerializer.Deserialize<JsonElement>(_tokenJson);

                if (_tokenData.TryGetProperty("refresh_token", out var refreshToken)) {
                    // Try to refresh the token
                    var newToken = await RefreshTokenAsync(clientId, clientSecret, refreshToken.GetString());
                    if (!string.IsNullOrEmpty(newToken)) return newToken;
                }
            }

            // No valid stored token, start new auth flow
            Console.WriteLine("Starting new authentication flow...");

            // Create a local server to receive the auth code
            var codeReceiver = new LocalServerCodeReceiver();
            var authorizationUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize" +
                                   $"?client_id={Uri.EscapeDataString(clientId)}" +
                                   $"&response_type=code" +
                                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                                   $"&scope={Uri.EscapeDataString("Files.ReadWrite offline_access")}";

            Console.WriteLine("Please open the following URL in your browser:");
            Console.WriteLine(authorizationUrl);

            // Start the local server and wait for the auth code
            var authCode = await codeReceiver.ReceiveCodeAsync(redirectUri, CancellationToken.None);

            if (string.IsNullOrEmpty(authCode)) throw new InvalidOperationException("No authorization code received");

            // Exchange the auth code for a token
            var tokenRequestContent = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", authCode),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            var tokenResponse = await _httpClient.PostAsync(
                "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                tokenRequestContent);

            tokenResponse.EnsureSuccessStatusCode();

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

            // Save the token data for future use
            await File.WriteAllTextAsync(tokenFilePath, tokenJson);

            // Return the access token
            return tokenData.GetProperty("access_token").GetString();
        }
        catch (Exception ex) {
            Console.WriteLine($"Authentication error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner error: {ex.InnerException.Message}");
            throw;
        }
    }

    public async Task RevokeTokenAsync(string userId) {
        try {
            var tokenFilePath = Path.Combine(_dataStorePath, $"onedrive_token_{userId}.json");
            if (File.Exists(tokenFilePath)) File.Delete(tokenFilePath);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error revoking token: {ex.Message}");
            throw;
        }
    }

    private async Task<string> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken) {
        try {
            var tokenRequestContent = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            });

            var tokenResponse = await _httpClient.PostAsync(
                "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                tokenRequestContent);

            if (!tokenResponse.IsSuccessStatusCode) return null;

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

            return tokenData.GetProperty("access_token").GetString();
        }
        catch {
            return null;
        }
    }

    // Simple implementation of a local server code receiver
    private class LocalServerCodeReceiver {
        public async Task<string> ReceiveCodeAsync(string redirectUri, CancellationToken cancellationToken) {
            // In a real implementation, this would start a local web server
            // For this example, we'll just prompt the user to enter the code manually

            Console.WriteLine("After authorizing the application, you will be redirected to a page with an authorization code.");
            Console.WriteLine("Please copy the code from the URL (after 'code=') and paste it here:");

            return Console.ReadLine();
        }
    }
}