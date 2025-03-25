using System.Text;
using CloudUnify.Core;
using CloudUnify.Core.Extensions;
using CloudUnify.Core.Models;
using CloudUnify.Core.Storage;

namespace CloudUnify.Console;

internal class Program {
    private const string ApplicationName = "CloudUnify";
    private const string TokenStorePath = "token_store";
    private const string ProviderStoragePath = "providers.json";

    private static readonly ProviderStorage _providerStorage = new(ProviderStoragePath);

    private static async Task Main(string[] args) {
        System.Console.WriteLine("CloudUnify Console Test Application");
        System.Console.WriteLine("==================================");

        // Initialize provider storage
        var providerStorage = new ProviderStorage(ProviderStoragePath);

        // Initialize CloudUnifyManager with provider storage
        var manager = new CloudUnifyManager(providerStorage);

        // Register providers from storage
        await RegisterStoredProviders(manager);

        // Auto-connect to previously connected providers
        await manager.AutoConnectProvidersAsync();

        try {
            // Display menu
            var exit = false;
            while (!exit) {
                System.Console.WriteLine("\nChoose an option:");
                System.Console.WriteLine("1. Connect to Google Drive");
                System.Console.WriteLine("2. List registered providers");
                System.Console.WriteLine("3. Connect to OneDrive");
                System.Console.WriteLine("4. List all files");
                System.Console.WriteLine("5. Upload a test file");
                System.Console.WriteLine("6. Download a file");
                System.Console.WriteLine("7. Get storage info");
                System.Console.WriteLine("8. Search files");
                System.Console.WriteLine("9. Exit");
                System.Console.Write("\nEnter your choice (1-9): ");

                var choice = System.Console.ReadLine();

                switch (choice) {
                    case "1":
                        await ConnectToGoogleDriveAsync(manager);
                        break;
                    case "2":
                        ListRegisteredProviders(manager);
                        break;
                    case "3":
                        await ConnectToOneDriveAsync(manager);
                        break;
                    case "4":
                        await ListAllFilesAsync(manager);
                        break;
                    case "5":
                        await UploadTestFileAsync(manager);
                        break;
                    case "6":
                        await DownloadFileAsync(manager);
                        break;
                    case "7":
                        await GetStorageInfoAsync(manager);
                        break;
                    case "8":
                        await SearchFilesAsync(manager);
                        break;
                    case "9":
                        exit = true;
                        break;
                    default:
                        System.Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null) System.Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
        }
    }

    private static async Task RegisterStoredProviders(CloudUnifyManager manager) {
        System.Console.WriteLine("Registering stored providers...");

        var providers = _providerStorage.GetAllProviders();

        if (providers.Count == 0) {
            System.Console.WriteLine("No stored providers found.");
            return;
        }

        foreach (var provider in providers)
            try {
                // Get the client secrets path from the provider metadata
                var clientSecretsPath = provider.ClientSecretsPath;

                if (string.IsNullOrEmpty(clientSecretsPath) || !File.Exists(clientSecretsPath)) {
                    System.Console.WriteLine($"Warning: Client secrets file not found for provider '{provider.Name}'. Skipping.");
                    continue;
                }

                // Connect the provider with the same ID based on its type
                if (provider.Type == "GoogleDrive") {
                    var (providerId, success) = await manager.ConnectGoogleDriveAsync(
                        clientSecretsPath,
                        ApplicationName,
                        TokenStorePath,
                        provider.UserId ?? string.Empty,
                        provider.Id
                    );

                    if (success)
                        System.Console.WriteLine($"Connected provider: {provider.Name} (ID: {providerId})");
                    else
                        System.Console.WriteLine($"Failed to connect provider: {provider.Name} (ID: {providerId})");
                }
                else if (provider.Type == "OneDrive") {
                    var (providerId, success) = await manager.ConnectOneDriveAsync(
                        clientSecretsPath,
                        ApplicationName,
                        TokenStorePath,
                        provider.UserId ?? string.Empty,
                        provider.Id
                    );

                    if (success)
                        System.Console.WriteLine($"Connected provider: {provider.Name} (ID: {providerId})");
                    else
                        System.Console.WriteLine($"Failed to connect provider: {provider.Name} (ID: {providerId})");
                }
            }
            catch (Exception ex) {
                System.Console.WriteLine($"Error connecting provider '{provider.Name}': {ex.Message}");
            }
    }

    private static void ListRegisteredProviders(CloudUnifyManager manager) {
        System.Console.WriteLine("\nRegistered Providers:");

        var providers = _providerStorage.GetAllProviders();

        if (providers.Count == 0) {
            System.Console.WriteLine("No providers registered yet.");
            return;
        }

        foreach (var provider in providers) {
            var isRegisteredWithManager = manager.HasProvider(provider.Id);

            System.Console.WriteLine($"- ID: {provider.Id}");
            System.Console.WriteLine($"  Name: {provider.Name}");
            System.Console.WriteLine($"  Type: {provider.Type}");
            System.Console.WriteLine($"  Added: {provider.AddedAt}");
            System.Console.WriteLine($"  Registered with manager: {(isRegisteredWithManager ? "Yes" : "No")}");
            System.Console.WriteLine($"  Connected: {(provider.IsConnected ? "Yes" : "No")}");
            if (provider.IsConnected && provider.UserId != null) {
                System.Console.WriteLine($"  User: {provider.UserId}");
                System.Console.WriteLine($"  Last Connected: {provider.LastConnected}");
            }

            System.Console.WriteLine();
        }
    }

    private static async Task ConnectToGoogleDriveAsync(CloudUnifyManager manager) {
        System.Console.WriteLine("\nConnecting to Google Drive...");

        // Ask for client secrets file path
        System.Console.Write("Enter path to client_secret.json file: ");
        var clientSecretsPath = System.Console.ReadLine();

        if (string.IsNullOrEmpty(clientSecretsPath) || !File.Exists(clientSecretsPath)) {
            System.Console.WriteLine("Invalid file path or file does not exist.");
            return;
        }

        System.Console.Write("Enter a name for this provider (e.g., 'Work Google Drive'): ");
        var name = System.Console.ReadLine() ?? "Google Drive";

        System.Console.Write("Enter user ID (e.g., your email): ");
        var userId = System.Console.ReadLine();

        if (string.IsNullOrEmpty(userId)) {
            System.Console.WriteLine("User ID cannot be empty.");
            return;
        }

        try {
            System.Console.WriteLine("Initiating OAuth2 flow. A browser window will open for authentication...");
            System.Console.WriteLine("Important: Make sure you have the following redirect URIs in your Google Cloud Console:");
            System.Console.WriteLine("- http://localhost");
            System.Console.WriteLine("- http://127.0.0.1");
            System.Console.WriteLine("- http://localhost:PORT (where PORT is any port number, e.g., 8080)");

            var (providerId, success) = await manager.ConnectGoogleDriveAsync(
                clientSecretsPath,
                ApplicationName,
                TokenStorePath,
                userId
            );

            if (success) {
                System.Console.WriteLine("Successfully connected to Google Drive!");
                // Save the provider information
                _providerStorage.SaveProvider(providerId, "GoogleDrive", name, userId, clientSecretsPath);
            }
            else {
                System.Console.WriteLine("Failed to connect to Google Drive.");
            }
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error connecting to Google Drive: {ex.Message}");
            if (ex.InnerException != null) System.Console.WriteLine($"Inner Error: {ex.InnerException.Message}");

            System.Console.WriteLine("\nTroubleshooting tips:");
            System.Console.WriteLine("1. Make sure your client_secret.json is valid and contains the correct credentials");
            System.Console.WriteLine("2. Add these redirect URIs to your Google Cloud Console:");
            System.Console.WriteLine("   - http://localhost");
            System.Console.WriteLine("   - http://127.0.0.1");
            System.Console.WriteLine("   - http://localhost:PORT (where PORT is any port number, e.g., 8080)");
            System.Console.WriteLine("3. For desktop applications, use OAuth client ID of type 'Desktop app' instead of 'Web application'");
            System.Console.WriteLine("4. Check that your application has the necessary API access enabled in Google Cloud Console");
            System.Console.WriteLine("5. Remember that changes to OAuth settings can take up to a few hours to propagate");
            System.Console.WriteLine("6. Try deleting the token_store directory to force a new authentication");
        }
    }

    private static async Task ConnectToOneDriveAsync(CloudUnifyManager manager) {
        System.Console.WriteLine("\nConnecting to OneDrive...");

        // Ask for client secrets file path
        System.Console.Write("Enter path to client_secret.json file: ");
        var clientSecretsPath = System.Console.ReadLine();

        if (string.IsNullOrEmpty(clientSecretsPath) || !File.Exists(clientSecretsPath)) {
            System.Console.WriteLine("Invalid file path or file does not exist.");
            return;
        }

        System.Console.Write("Enter a name for this provider (e.g., 'Personal OneDrive'): ");
        var name = System.Console.ReadLine() ?? "OneDrive";

        System.Console.Write("Enter user ID (e.g., your email): ");
        var userId = System.Console.ReadLine();

        if (string.IsNullOrEmpty(userId)) {
            System.Console.WriteLine("User ID cannot be empty.");
            return;
        }

        try {
            System.Console.WriteLine("Initiating OAuth2 flow. A browser window will open for authentication...");
            System.Console.WriteLine("Important: Make sure you have the following redirect URIs in your Microsoft Azure Portal:");
            System.Console.WriteLine("- http://localhost");
            System.Console.WriteLine("- http://127.0.0.1");
            System.Console.WriteLine("- http://localhost:PORT (where PORT is any port number, e.g., 8080)");

            var (providerId, success) = await manager.ConnectOneDriveAsync(
                clientSecretsPath,
                ApplicationName,
                TokenStorePath,
                userId
            );

            if (success) {
                System.Console.WriteLine("Successfully connected to OneDrive!");
                // Save the provider information
                _providerStorage.SaveProvider(providerId, "OneDrive", name, userId, clientSecretsPath);
            }
            else {
                System.Console.WriteLine("Failed to connect to OneDrive.");
            }
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error connecting to OneDrive: {ex.Message}");
            if (ex.InnerException != null) System.Console.WriteLine($"Inner Error: {ex.InnerException.Message}");

            System.Console.WriteLine("\nTroubleshooting tips:");
            System.Console.WriteLine("1. Make sure your client_secret.json is valid and contains the correct credentials");
            System.Console.WriteLine("2. Add these redirect URIs to your Microsoft Azure Portal:");
            System.Console.WriteLine("   - http://localhost");
            System.Console.WriteLine("   - http://127.0.0.1");
            System.Console.WriteLine("   - http://localhost:PORT (where PORT is any port number, e.g., 8080)");
            System.Console.WriteLine("3. Check that your application has the necessary API permissions enabled in Azure Portal");
            System.Console.WriteLine("4. Remember that changes to OAuth settings can take up to a few hours to propagate");
            System.Console.WriteLine("5. Try deleting the token_store directory to force a new authentication");
        }
    }

    private static async Task ListAllFilesAsync(CloudUnifyManager manager) {
        System.Console.WriteLine("\nListing all files...");

        try {
            var files = await manager.ListAllFilesAsync();

            System.Console.WriteLine($"Found {files.Count} files across all connected drives:");

            foreach (var file in files) {
                System.Console.WriteLine($"- {file.Name} ({FormatFileSize(file.Size)})");
                System.Console.WriteLine($"  Path: {file.Path}");
                System.Console.WriteLine($"  Provider: {file.ProviderName}");
                System.Console.WriteLine($"  ID: {file.Id}");
                System.Console.WriteLine($"  Modified: {file.ModifiedAt}");
                System.Console.WriteLine();
            }
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error listing files: {ex.Message}");
        }
    }

    private static async Task UploadTestFileAsync(CloudUnifyManager manager) {
        System.Console.WriteLine("\nUploading a test file...");

        // List available providers
        var providers = _providerStorage.GetAllProviders();
        var connectedProviders = new List<ProviderInfo>();

        // Filter to only include providers that are connected and registered with the manager
        foreach (var provider in providers)
            if (provider.IsConnected && manager.HasProvider(provider.Id))
                connectedProviders.Add(provider);

        if (connectedProviders.Count == 0) {
            System.Console.WriteLine("No connected providers available. Please connect to a provider first.");
            return;
        }

        System.Console.WriteLine("Available connected providers:");
        for (var i = 0; i < connectedProviders.Count; i++)
            System.Console.WriteLine($"{i + 1}. {connectedProviders[i].Name} ({connectedProviders[i].Id})");

        System.Console.Write("Select a provider (number): ");
        if (!int.TryParse(System.Console.ReadLine(), out var providerIndex) || providerIndex < 1 || providerIndex > connectedProviders.Count) {
            System.Console.WriteLine("Invalid selection.");
            return;
        }

        var selectedProvider = connectedProviders[providerIndex - 1];
        var providerId = selectedProvider.Id;

        try {
            // Create a test file content
            var content = Encoding.UTF8.GetBytes("This is a test file created by CloudUnify Console application.");

            // Upload the file
            var uploadedFile = await manager.UploadFileAsync(content, "test_file.txt", "/", providerId);

            System.Console.WriteLine("File uploaded successfully:");
            System.Console.WriteLine($"- Name: {uploadedFile.Name}");
            System.Console.WriteLine($"- ID: {uploadedFile.Id}");
            System.Console.WriteLine($"- Path: {uploadedFile.Path}");
            System.Console.WriteLine($"- Size: {FormatFileSize(uploadedFile.Size)}");
            System.Console.WriteLine($"- Web Link: {uploadedFile.WebViewLink}");
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error uploading file: {ex.Message}");
        }
    }

    private static async Task DownloadFileAsync(CloudUnifyManager manager) {
        System.Console.WriteLine("\nDownloading a file...");

        // List available providers
        var providers = _providerStorage.GetAllProviders();
        var connectedProviders = new List<ProviderInfo>();

        // Filter to only include providers that are connected and registered with the manager
        foreach (var provider in providers)
            if (provider.IsConnected && manager.HasProvider(provider.Id))
                connectedProviders.Add(provider);

        if (connectedProviders.Count == 0) {
            System.Console.WriteLine("No connected providers available. Please connect to a provider first.");
            return;
        }

        System.Console.WriteLine("Available connected providers:");
        for (var i = 0; i < connectedProviders.Count; i++)
            System.Console.WriteLine($"{i + 1}. {connectedProviders[i].Name} ({connectedProviders[i].Id})");

        System.Console.Write("Select a provider (number): ");
        if (!int.TryParse(System.Console.ReadLine(), out var providerIndex) || providerIndex < 1 || providerIndex > connectedProviders.Count) {
            System.Console.WriteLine("Invalid selection.");
            return;
        }

        var selectedProvider = connectedProviders[providerIndex - 1];
        var providerId = selectedProvider.Id;

        System.Console.Write("Enter file ID to download: ");
        var fileId = System.Console.ReadLine();

        if (string.IsNullOrEmpty(fileId)) {
            System.Console.WriteLine("File ID cannot be empty.");
            return;
        }

        try {
            // Get file info
            var fileInfo = await manager.GetFileAsync(fileId, providerId);

            if (fileInfo == null) {
                System.Console.WriteLine("File not found.");
                return;
            }

            // Download the file
            var content = await manager.DownloadFileAsync(fileId, providerId);

            // Save to disk
            var downloadPath = Path.Combine(Environment.CurrentDirectory, fileInfo.Name);
            File.WriteAllBytes(downloadPath, content);

            System.Console.WriteLine($"File downloaded successfully to: {downloadPath}");
            System.Console.WriteLine($"- Name: {fileInfo.Name}");
            System.Console.WriteLine($"- Size: {FormatFileSize(fileInfo.Size)}");
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error downloading file: {ex.Message}");
        }
    }

    private static async Task GetStorageInfoAsync(CloudUnifyManager manager) {
        System.Console.WriteLine("\nGetting storage information...");

        try {
            var storageInfoList = await manager.GetStorageInfoAsync();

            if (storageInfoList.Count == 0) {
                System.Console.WriteLine("No storage providers connected.");
                return;
            }

            foreach (var storageInfo in storageInfoList) {
                System.Console.WriteLine($"Provider: {storageInfo.ProviderName}");
                System.Console.WriteLine($"User: {storageInfo.UserEmail}");
                System.Console.WriteLine($"Used: {FormatFileSize(storageInfo.UsedSpace)}");
                System.Console.WriteLine($"Total: {FormatFileSize(storageInfo.TotalSpace)}");
                System.Console.WriteLine($"Available: {FormatFileSize(storageInfo.AvailableSpace)}");
                System.Console.WriteLine($"Usage: {storageInfo.UsagePercentage:F2}%");
                System.Console.WriteLine();
            }
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error getting storage info: {ex.Message}");
        }
    }

    private static async Task SearchFilesAsync(CloudUnifyManager manager) {
        System.Console.WriteLine("\nSearching files...");
        System.Console.Write("Enter search term: ");
        var searchTerm = System.Console.ReadLine() ?? "";

        try {
            // Use the manager's search method directly
            var options = new SearchOptions {
                Path = "/",
                SortBy = "name",
                SortDirection = SortDirection.Ascending
            };

            var files = await manager.SearchFilesAsync(searchTerm, options);

            System.Console.WriteLine($"Found {files.Count} files matching '{searchTerm}':");

            foreach (var file in files) {
                System.Console.WriteLine($"- {file.Name} ({FormatFileSize(file.Size)})");
                System.Console.WriteLine($"  Path: {file.Path}");
                System.Console.WriteLine($"  Provider: {file.ProviderName}");
                System.Console.WriteLine($"  Modified: {file.ModifiedAt}");
                System.Console.WriteLine();
            }
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error searching files: {ex.Message}");
        }
    }

    private static string FormatFileSize(long bytes) {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        var counter = 0;
        decimal number = bytes;

        while (Math.Round(number / 1024) >= 1) {
            number /= 1024;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }
}