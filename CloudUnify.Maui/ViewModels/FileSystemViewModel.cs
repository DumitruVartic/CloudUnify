using System.Collections.ObjectModel;
using CloudUnify.Maui.Models;

namespace CloudUnify.Maui.ViewModels;

public class FileSystemViewModel
{
    private Dictionary<string, List<FileSystemItem>> _mockFileSystem;
    private Dictionary<string, Dictionary<string, List<FileSystemItem>>> _providerFileSystems;
    public ObservableCollection<FileSystemItem> Items { get; set; }
    public string CurrentPath { get; private set; }
    public bool IsGridView { get; set; } = true;
    public bool IsMultiSelect { get; set; }
    public bool IsUnifiedView { get; set; }
    public string CurrentProvider { get; private set; }
    public IEnumerable<FileSystemItem> SelectedItems => Items.Where(i => i.IsSelected);
    public List<(string Name, string Path)> BreadcrumbItems { get; private set; }

    public FileSystemViewModel()
    {
        CurrentPath = "/";
        Items = new ObservableCollection<FileSystemItem>();
        BreadcrumbItems = new List<(string Name, string Path)>();
        InitializeMockFileSystem();
        LoadCurrentFolder();
    }

    private void InitializeMockFileSystem()
    {
        _mockFileSystem = new Dictionary<string, List<FileSystemItem>>();
        _providerFileSystems = new Dictionary<string, Dictionary<string, List<FileSystemItem>>>();

        // Initialize provider-specific file systems
        InitializeOneDriveFiles();
        InitializeGoogleDriveFiles();
        InitializeDropboxFiles();

        // Initialize unified view
        InitializeUnifiedView();
    }

    private void InitializeOneDriveFiles()
    {
        var oneDrive = new Dictionary<string, List<FileSystemItem>>();
        
        oneDrive["/"] = new List<FileSystemItem>
        {
            new() { Name = "Documents", Path = "/Documents", IsDirectory = true, LastModified = DateTime.Now.AddDays(-1), Provider = "OneDrive" },
            new() { Name = "report.pdf", Path = "/report.pdf", IsDirectory = false, Size = 1024 * 1024, LastModified = DateTime.Now.AddHours(-2), Provider = "OneDrive" }
        };

        oneDrive["/Documents"] = new List<FileSystemItem>
        {
            new() { Name = "presentation.pptx", Path = "/Documents/presentation.pptx", IsDirectory = false, Size = 2L * 1024 * 1024, LastModified = DateTime.Now.AddHours(-1), Provider = "OneDrive" }
        };

        _providerFileSystems["OneDrive"] = oneDrive;
    }

    private void InitializeGoogleDriveFiles()
    {
        var googleDrive = new Dictionary<string, List<FileSystemItem>>();
        
        googleDrive["/"] = new List<FileSystemItem>
        {
            new() { Name = "Projects", Path = "/Projects", IsDirectory = true, LastModified = DateTime.Now.AddDays(-3), Provider = "GoogleDrive" },
            new() { Name = "spreadsheet.xlsx", Path = "/spreadsheet.xlsx", IsDirectory = false, Size = (long)(1.5 * 1024 * 1024), LastModified = DateTime.Now.AddHours(-4), Provider = "GoogleDrive" }
        };

        googleDrive["/Projects"] = new List<FileSystemItem>
        {
            new() { Name = "project-docs.docx", Path = "/Projects/project-docs.docx", IsDirectory = false, Size = 800 * 1024, LastModified = DateTime.Now.AddDays(-1), Provider = "GoogleDrive" }
        };

        _providerFileSystems["GoogleDrive"] = googleDrive;
    }

    private void InitializeDropboxFiles()
    {
        var dropbox = new Dictionary<string, List<FileSystemItem>>();
        
        dropbox["/"] = new List<FileSystemItem>
        {
            new() { Name = "Photos", Path = "/Photos", IsDirectory = true, LastModified = DateTime.Now.AddDays(-2), Provider = "Dropbox" },
            new() { Name = "backup.zip", Path = "/backup.zip", IsDirectory = false, Size = (long)(2.5 * 1024 * 1024), LastModified = DateTime.Now.AddHours(-12), Provider = "Dropbox" }
        };

        dropbox["/Photos"] = new List<FileSystemItem>
        {
            new() { Name = "vacation.jpg", Path = "/Photos/vacation.jpg", IsDirectory = false, Size = 500 * 1024, LastModified = DateTime.Now.AddDays(-2), Provider = "Dropbox" }
        };

        _providerFileSystems["Dropbox"] = dropbox;
    }

    private void InitializeUnifiedView()
    {
        _mockFileSystem["/"] = new List<FileSystemItem>();
        
        // Combine root items from all providers
        foreach (var (provider, fileSystem) in _providerFileSystems)
        {
            if (fileSystem.TryGetValue("/", out var rootItems))
            {
                _mockFileSystem["/"].AddRange(rootItems);
            }
        }

        // Add items from each provider's folders
        foreach (var (provider, fileSystem) in _providerFileSystems)
        {
            foreach (var (path, items) in fileSystem.Where(kvp => kvp.Key != "/"))
            {
                _mockFileSystem[path] = items;
            }
        }
    }

    private void LoadCurrentFolder()
    {
        Items.Clear();
        
        if (IsUnifiedView)
        {
            if (_mockFileSystem.TryGetValue(CurrentPath, out var items))
            {
                foreach (var item in items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name))
                {
                    Items.Add(item);
                }
            }
        }
        else if (!string.IsNullOrEmpty(CurrentProvider) && 
                 _providerFileSystems.TryGetValue(CurrentProvider, out var providerSystem) &&
                 providerSystem.TryGetValue(CurrentPath, out var providerItems))
        {
            foreach (var item in providerItems.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name))
            {
                Items.Add(item);
            }
        }

        UpdateBreadcrumbs();
    }

    public void SetProvider(string provider)
    {
        if (_providerFileSystems.ContainsKey(provider))
        {
            CurrentProvider = provider;
            CurrentPath = "/";
            LoadCurrentFolder();
        }
    }

    private void UpdateBreadcrumbs()
    {
        BreadcrumbItems.Clear();
        
        if (!IsUnifiedView && !string.IsNullOrEmpty(CurrentProvider))
        {
            BreadcrumbItems.Add((CurrentProvider, "/"));
        }
        else
        {
            BreadcrumbItems.Add(("Home", "/"));
        }

        if (CurrentPath == "/") return;

        var parts = CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = "";
        foreach (var part in parts)
        {
            currentPath += "/" + part;
            BreadcrumbItems.Add((part, currentPath));
        }
    }

    public void NavigateToFolder(string path)
    {
        if (_mockFileSystem.ContainsKey(path))
        {
            CurrentPath = path;
            LoadCurrentFolder();
            ClearSelection();
        }
    }

    public void NavigateUp()
    {
        if (CurrentPath != "/")
        {
            var parentPath = Path.GetDirectoryName(CurrentPath)?.Replace("\\", "/") ?? "/";
            NavigateToFolder(parentPath);
        }
    }

    public void ToggleItemSelection(FileSystemItem item)
    {
        if (!IsMultiSelect)
        {
            foreach (var i in Items.Where(i => i != item))
            {
                i.IsSelected = false;
            }
        }
        item.IsSelected = !item.IsSelected;
    }

    public void ClearSelection()
    {
        foreach (var item in Items)
        {
            item.IsSelected = false;
        }
    }

    public void ToggleViewMode()
    {
        IsGridView = !IsGridView;
    }

    public void DeleteSelected()
    {
        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        foreach (var item in selectedItems)
        {
            Items.Remove(item);
            // In a real implementation, we would also update _mockFileSystem
        }
    }

    public void CreateNewFolder(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var newPath = Path.Combine(CurrentPath, name).Replace("\\", "/");
        var newFolder = new FileSystemItem
        {
            Name = name,
            Path = newPath,
            IsDirectory = true,
            LastModified = DateTime.Now
        };

        Items.Add(newFolder);
        _mockFileSystem[CurrentPath].Add(newFolder);
        _mockFileSystem[newPath] = new List<FileSystemItem>();
    }

    public void RenameItem(FileSystemItem item, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        
        var existingItem = Items.FirstOrDefault(i => i == item);
        if (existingItem != null)
        {
            var oldPath = existingItem.Path;
            var newPath = Path.Combine(Path.GetDirectoryName(existingItem.Path) ?? "/", newName).Replace("\\", "/");
            
            existingItem.Name = newName;
            existingItem.Path = newPath;

            // In a real implementation, we would also update _mockFileSystem
        }
    }
} 