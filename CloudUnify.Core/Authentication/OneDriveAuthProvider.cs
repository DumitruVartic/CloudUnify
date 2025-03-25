using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

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

        // Create token store directory if it doesn't exist
        if (!Directory.Exists(_dataStorePath)) {
            Directory.CreateDirectory(_dataStorePath);
        }
    }

    public async Task<string> AuthenticateAsync(string userId) {
        try {
            Console.WriteLine("Starting OneDrive authentication process...");

            // Load client secrets
            string clientId;
            string clientSecret;
            string redirectUri;
            string tokenFilePath = Path.Combine(_dataStorePath, $"onedrive_token_{userId}.json");

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
                                   $"&scope={Uri.EscapeDataString("Files.ReadWrite Files.ReadWrite.All Files.Read Files.Read.All offline_access User.Read")}" +
                                   $"&response_mode=query";

            Console.WriteLine("A browser window will open for authentication...");
            Console.WriteLine("Please log in and grant the requested permissions.");

            // Start the local server and wait for the auth code
            var authCode = await codeReceiver.ReceiveCodeAsync(redirectUri, CancellationToken.None);

            if (string.IsNullOrEmpty(authCode)) throw new InvalidOperationException("No authorization code received");

            // Exchange the auth code for a token
            var tokenRequestContent = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", authCode),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("scope", "Files.ReadWrite Files.ReadWrite.All Files.Read Files.Read.All offline_access User.Read")
            });

            Console.WriteLine("Exchanging authorization code for access token...");
            var tokenResponse = await _httpClient.PostAsync(
                "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                tokenRequestContent);

            var responseContent = await tokenResponse.Content.ReadAsStringAsync();
            if (!tokenResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Token exchange failed. Status code: {tokenResponse.StatusCode}");
                Console.WriteLine($"Response content: {responseContent}");
                tokenResponse.EnsureSuccessStatusCode(); // This will throw with the actual error
            }

            var tokenData = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Ensure directory exists before saving
            var tokenDirectory = Path.GetDirectoryName(tokenFilePath);
            if (!string.IsNullOrEmpty(tokenDirectory) && !Directory.Exists(tokenDirectory)) {
                Directory.CreateDirectory(tokenDirectory);
            }

            // Save the token data for future use
            await File.WriteAllTextAsync(tokenFilePath, responseContent);

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

    // Implementation of a local server code receiver
    private class LocalServerCodeReceiver {
        private HttpListener? _listener;
        private TaskCompletionSource<string>? _codeReceived;
        private readonly object _lock = new();

        public async Task<string> ReceiveCodeAsync(string redirectUri, CancellationToken cancellationToken) {
            _codeReceived = new TaskCompletionSource<string>();

            // Parse the redirect URI to get the port
            var uri = new Uri(redirectUri);
            var port = uri.Port;

            // Start the HTTP listener
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();

            // Start listening for requests in the background
            _ = ListenForRequestsAsync();

            // Open the browser
            var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize" +
                         $"?client_id={Uri.EscapeDataString("526fa3d0-5a52-4a99-9c74-58dbbba1ca57")}" +
                         $"&response_type=code" +
                         $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                         $"&scope={Uri.EscapeDataString("Files.ReadWrite Files.ReadWrite.All Files.Read Files.Read.All offline_access User.Read")}" +
                         $"&response_mode=query";

            try {
                Process.Start(new ProcessStartInfo {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) {
                Console.WriteLine($"Failed to open browser automatically: {ex.Message}");
                Console.WriteLine("Please open this URL manually:");
                Console.WriteLine(authUrl);
            }

            // Wait for the code to be received
            return await _codeReceived.Task;
        }

        private async Task ListenForRequestsAsync() {
            try {
                while (_listener != null && _listener.IsListening) {
                    var context = await _listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    // Check if this is the OAuth callback
                    if (request.Url?.Query.StartsWith("?code=") == true) {
                        var code = request.Url.Query.Substring(6); // Remove "?code="
                        _codeReceived?.TrySetResult(code);

                        // Send a success response to the browser
                        var successHtml = "<html><body><h1>Authentication Successful!</h1><p>You can close this window and return to the application.</p></body></html>";
                        var buffer = System.Text.Encoding.UTF8.GetBytes(successHtml);
                        response.ContentType = "text/html";
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    else {
                        // Send a 404 response for other requests
                        response.StatusCode = 404;
                        response.Close();
                    }
                }
            }
            catch (Exception ex) {
                _codeReceived?.TrySetException(ex);
            }
            finally {
                _listener?.Stop();
                _listener?.Close();
            }
        }
    }
}