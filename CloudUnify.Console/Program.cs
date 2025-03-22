﻿using System.Text;
using CloudUnify.Core;
using CloudUnify.Core.Extensions;

namespace CloudUnify.Console;

internal class Program {
    private const string ApplicationName = "CloudUnify";
    private const string TokenStorePath = "token_store";
    private const string ProviderStoragePath = "providers.json";

    private static readonly ProviderStorage _providerStorage = new(ProviderStoragePath);

    private static async Task Main(string[] args) {
        System.Console.WriteLine("CloudUnify Console Test Application");
        System.Console.WriteLine("==================================");

        // Initialize CloudUnifyManager
        var manager = new CloudUnifyManager();

        // Auto-connect to previously connected providers
        await AutoConnectProvidersAsync(manager);

        try {
            // Display menu
            var exit = false;
            while (!exit) {
                System.Console.WriteLine("\nChoose an option:");
                System.Console.WriteLine("1. Register Google Drive provider");
                System.Console.WriteLine("2. List registered providers");
                System.Console.WriteLine("3. Connect to Google Drive");
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
                        RegisterGoogleDriveProvider(manager);
                        break;
                    case "2":
                        ListRegisteredProviders();
                        break;
                    case "3":
                        await ConnectToGoogleDriveAsync(manager);
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

    private static async Task AutoConnectProvidersAsync(CloudUnifyManager manager) {
        System.Console.WriteLine("Checking for previously connected providers...");

        var connectedProviders = _providerStorage.GetConnectedProviders();

        if (connectedProviders.Count == 0) {
            System.Console.WriteLine("No previously connected providers found.");
            return;
        }

        System.Console.WriteLine($"Found {connectedProviders.Count} previously connected providers. Attempting to reconnect...");

        foreach (var provider in connectedProviders) {
            if (string.IsNullOrEmpty(provider.UserId)) {
                System.Console.WriteLine($"Skipping provider '{provider.Name}' (no user ID stored)");
                continue;
            }

            System.Console.WriteLine($"Reconnecting to {provider.Name} as {provider.UserId}...");

            try {
                var connected = await manager.ConnectGoogleDriveAsync(provider.Id, provider.UserId);

                if (connected) {
                    System.Console.WriteLine($"Successfully reconnected to {provider.Name}!");
                    _providerStorage.UpdateConnectionState(provider.Id, true);
                }
                else {
                    System.Console.WriteLine($"Failed to reconnect to {provider.Name}.");
                    _providerStorage.UpdateConnectionState(provider.Id, false);
                }
            }
            catch (Exception ex) {
                System.Console.WriteLine($"Error reconnecting to {provider.Name}: {ex.Message}");
                _providerStorage.UpdateConnectionState(provider.Id, false);
            }
        }

        System.Console.WriteLine("Auto-connection process completed.");
    }

    private static void RegisterGoogleDriveProvider(CloudUnifyManager manager) {
        System.Console.WriteLine("\nRegistering Google Drive provider...");

        // Ask for client secrets file path
        System.Console.Write("Enter path to client_secret.json file: ");
        var clientSecretsPath = System.Console.ReadLine();

        if (string.IsNullOrEmpty(clientSecretsPath) || !File.Exists(clientSecretsPath)) {
            System.Console.WriteLine("Invalid file path or file does not exist.");
            return;
        }

        System.Console.Write("Enter a name for this provider (e.g., 'Work Google Drive'): ");
        var name = System.Console.ReadLine() ?? "Google Drive";

        try {
            // Register the provider and get the ID
            var providerId = manager.RegisterGoogleDriveProvider(
                clientSecretsPath,
                ApplicationName,
                TokenStorePath
            );

            // Save the provider information
            _providerStorage.SaveProvider(providerId, "GoogleDrive", name);

            System.Console.WriteLine($"Provider registered successfully with ID: {providerId}");
            System.Console.WriteLine("Client secrets loaded successfully.");
        }
        catch (Exception ex) {
            System.Console.WriteLine($"Error registering provider: {ex.Message}");
            if (ex.InnerException != null) System.Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
        }
    }

    private static void ListRegisteredProviders() {
        System.Console.WriteLine("\nRegistered Providers:");

        var providers = _providerStorage.GetAllProviders();

        if (providers.Count == 0) {
            System.Console.WriteLine("No providers registered yet.");
            return;
        }

        foreach (var provider in providers) {
            System.Console.WriteLine($"- ID: {provider.Id}");
            System.Console.WriteLine($"  Name: {provider.Name}");
            System.Console.WriteLine($"  Type: {provider.Type}");
            System.Console.WriteLine($"  Added: {provider.AddedAt}");
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

        // List available providers
        var providers = _providerStorage.GetAllProviders();

        if (providers.Count == 0) {
            System.Console.WriteLine("No providers registered yet. Please register a provider first.");
            return;
        }

        System.Console.WriteLine("Available providers:");
        for (var i = 0; i < providers.Count; i++) {
            var connectionStatus = providers[i].IsConnected ? " (Connected)" : "";
            System.Console.WriteLine($"{i + 1}. {providers[i].Name}{connectionStatus} ({providers[i].Id})");
        }

        System.Console.Write("Select a provider (number): ");
        if (!int.TryParse(System.Console.ReadLine(), out var providerIndex) || providerIndex < 1 || providerIndex > providers.Count) {
            System.Console.WriteLine("Invalid selection.");
            return;
        }

        var selectedProvider = providers[providerIndex - 1];
        var providerId = selectedProvider.Id;

        // Check if we already have a user ID for this provider
        var userId = selectedProvider.UserId;

        if (string.IsNullOrEmpty(userId)) {
            System.Console.Write("Enter user ID (e.g., your email): ");
            userId = System.Console.ReadLine();

            if (string.IsNullOrEmpty(userId)) {
                System.Console.WriteLine("User ID cannot be empty.");
                return;
            }
        }
        else {
            System.Console.WriteLine($"Using stored user ID: {userId}");
            System.Console.Write("Use a different user ID? (y/n): ");
            var changeUser = System.Console.ReadLine()?.ToLower();

            if (changeUser == "y" || changeUser == "yes") {
                System.Console.Write("Enter new user ID: ");
                userId = System.Console.ReadLine();

                if (string.IsNullOrEmpty(userId)) {
                    System.Console.WriteLine("User ID cannot be empty.");
                    return;
                }
            }
        }

        try {
            System.Console.WriteLine("Initiating OAuth2 flow. A browser window will open for authentication...");
            System.Console.WriteLine("Important: Make sure you have the following redirect URIs in your Google Cloud Console:");
            System.Console.WriteLine("- http://localhost");
            System.Console.WriteLine("- http://127.0.0.1");
            System.Console.WriteLine("- http://localhost:PORT (where PORT is any port number, e.g., 8080)");

            var connected = await manager.ConnectGoogleDriveAsync(providerId, userId);

            if (connected) {
                System.Console.WriteLine("Successfully connected to Google Drive!");
                // Update the provider storage with connection state and user ID
                _providerStorage.SaveProvider(providerId, selectedProvider.Type, selectedProvider.Name, userId);
            }
            else {
                System.Console.WriteLine("Failed to connect to Google Drive.");
                _providerStorage.UpdateConnectionState(providerId, false);
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
        var connectedProviders = providers.FindAll(p => p.IsConnected);

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
        var connectedProviders = providers.FindAll(p => p.IsConnected);

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
            // Create a CloudUnify instance and register providers from the manager
            // This is a workaround since we don't have direct access to the CloudUnify instance in the manager
            var cloudUnify = new Core.CloudUnify();
            var options = new SearchOptions {
                Path = "/",
                SortBy = "name",
                SortDirection = SortDirection.Ascending
            };

            // Use extension method to search files
            var files = await cloudUnify.SearchFilesAsync(searchTerm, options);

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