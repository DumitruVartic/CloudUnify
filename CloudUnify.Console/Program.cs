using CloudUnify.Core;

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

        try {
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
                        await
                            DownloadFileAsync(manager);
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

    private static async Task SearchFilesAsync(CloudUnifyManager manager) {
        throw new NotImplementedException();
    }

    private static async Task ConnectToGoogleDriveAsync(CloudUnifyManager manager) {
        throw new NotImplementedException();
    }

    private static async Task GetStorageInfoAsync(CloudUnifyManager manager) {
        throw new NotImplementedException();
    }

    private static async Task DownloadFileAsync(CloudUnifyManager manager) {
        throw new NotImplementedException();
    }

    private static async Task UploadTestFileAsync(CloudUnifyManager manager) {
        throw new NotImplementedException();
    }

    private static async Task ListAllFilesAsync(CloudUnifyManager manager) {
        throw new NotImplementedException();
    }

    private static void ListRegisteredProviders() {
        throw new NotImplementedException();
    }

    private static void RegisterGoogleDriveProvider(CloudUnifyManager manager) {
        throw new NotImplementedException();
    }
}