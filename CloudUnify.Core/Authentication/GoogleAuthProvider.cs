using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace CloudUnify.Core.Authentication;

public class GoogleAuthProvider : IAuthProvider {
    private readonly string _applicationName;
    private readonly string _clientSecretsPath;
    private readonly string _dataStorePath;

    public GoogleAuthProvider(string clientSecretsPath, string applicationName, string dataStorePath) {
        _clientSecretsPath = clientSecretsPath;
        _applicationName = applicationName;
        _dataStorePath = dataStorePath;
    }

    public async Task<string> AuthenticateAsync(string userId) {
        try {
            Console.WriteLine("Starting authentication process...");

            UserCredential credential;

            using (var stream = new FileStream(_clientSecretsPath, FileMode.Open, FileAccess.Read)) {
                var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer {
                    ClientSecrets = secrets,
                    Scopes = new[] { DriveService.Scope.Drive },
                    DataStore = new FileDataStore(_dataStorePath, true)
                });

                // LocalServerCodeReceiver handles the OAuth callback
                var codeReceiver = new LocalServerCodeReceiver();

                var authCode = new AuthorizationCodeInstalledApp(flow, codeReceiver);

                Console.WriteLine("Requesting authorization...");
                Console.WriteLine("A browser window will open. Please log in and grant the requested permissions.");
                
                // Force a new authorization by using a unique user ID
                var uniqueUserId = $"{userId}_{Guid.NewGuid()}";
                credential = await authCode.AuthorizeAsync(uniqueUserId, CancellationToken.None);
                Console.WriteLine("Authorization completed successfully.");
            }

            var service = new DriveService(new BaseClientService.Initializer {
                HttpClientInitializer = credential,
                ApplicationName = _applicationName
            });

            Console.WriteLine($"Authentication successful. User: {userId}");

            return credential.Token.AccessToken;
        }
        catch (Exception ex) {
            Console.WriteLine($"Authentication error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner error: {ex.InnerException.Message}");
            throw;
        }
    }

    public async Task RevokeTokenAsync(string userId) {
        try {
            using (var stream = new FileStream(_clientSecretsPath, FileMode.Open, FileAccess.Read)) {
                var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer {
                    ClientSecrets = secrets,
                    Scopes = new[] { DriveService.Scope.Drive },
                    DataStore = new FileDataStore(_dataStorePath)
                });

                await flow.RevokeTokenAsync(userId, null, CancellationToken.None);
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error revoking token: {ex.Message}");
            throw;
        }
    }
}