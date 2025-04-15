using System.Collections.ObjectModel;
using CloudUnify.Maui.Models;

namespace CloudUnify.Maui.ViewModels;

public class FileSystemViewModel
{
    private Dictionary<string, List<FileSystemItem>> _mockFileSystem;
    public ObservableCollection<FileSystemItem> Items { get; set; }
    public string CurrentPath { get; private set; }
    public bool IsGridView { get; set; } = true;
    public bool IsMultiSelect { get; set; }
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

        // Root directory
        _mockFileSystem["/"] = new List<FileSystemItem>
        {
            new() { Name = "Documents", Path = "/Documents", IsDirectory = true, LastModified = DateTime.Now.AddDays(-1) },
            new() { Name = "Pictures", Path = "/Pictures", IsDirectory = true, LastModified = DateTime.Now.AddDays(-2) },
            new() { Name = "Work", Path = "/Work", IsDirectory = true, LastModified = DateTime.Now.AddHours(-5) },
            new() { Name = "report.pdf", Path = "/report.pdf", IsDirectory = false, Size = 1024 * 1024, LastModified = DateTime.Now.AddHours(-2) }
        };

        // Documents folder
        _mockFileSystem["/Documents"] = new List<FileSystemItem>
        {
            new() { Name = "Projects", Path = "/Documents/Projects", IsDirectory = true, LastModified = DateTime.Now.AddDays(-1) },
            new() { Name = "presentation.pptx", Path = "/Documents/presentation.pptx", IsDirectory = false, Size = 2L * 1024 * 1024, LastModified = DateTime.Now.AddHours(-1) },
            new() { Name = "document.docx", Path = "/Documents/document.docx", IsDirectory = false, Size = 750 * 1024, LastModified = DateTime.Now.AddMinutes(-45) }
        };

        // Pictures folder
        _mockFileSystem["/Pictures"] = new List<FileSystemItem>
        {
            new() { Name = "Vacation", Path = "/Pictures/Vacation", IsDirectory = true, LastModified = DateTime.Now.AddDays(-5) },
            new() { Name = "image1.jpg", Path = "/Pictures/image1.jpg", IsDirectory = false, Size = 500 * 1024, LastModified = DateTime.Now.AddMinutes(-30) },
            new() { Name = "image2.png", Path = "/Pictures/image2.png", IsDirectory = false, Size = (long)(1.5 * 1024 * 1024), LastModified = DateTime.Now.AddHours(-3) }
        };

        // Work folder
        _mockFileSystem["/Work"] = new List<FileSystemItem>
        {
            new() { Name = "Meetings", Path = "/Work/Meetings", IsDirectory = true, LastModified = DateTime.Now.AddDays(-1) },
            new() { Name = "project-plan.xlsx", Path = "/Work/project-plan.xlsx", IsDirectory = false, Size = (long)(1.2 * 1024 * 1024), LastModified = DateTime.Now.AddHours(-4) }
        };

        // Nested folders
        _mockFileSystem["/Documents/Projects"] = new List<FileSystemItem>
        {
            new() { Name = "project1.docx", Path = "/Documents/Projects/project1.docx", IsDirectory = false, Size = 800 * 1024, LastModified = DateTime.Now.AddDays(-1) }
        };

        _mockFileSystem["/Pictures/Vacation"] = new List<FileSystemItem>
        {
            new() { Name = "beach.jpg", Path = "/Pictures/Vacation/beach.jpg", IsDirectory = false, Size = (long)(2.5 * 1024 * 1024), LastModified = DateTime.Now.AddDays(-5) }
        };

        _mockFileSystem["/Work/Meetings"] = new List<FileSystemItem>
        {
            new() { Name = "notes.txt", Path = "/Work/Meetings/notes.txt", IsDirectory = false, Size = 50 * 1024, LastModified = DateTime.Now.AddHours(-2) }
        };
    }

    private void LoadCurrentFolder()
    {
        Items.Clear();
        if (_mockFileSystem.TryGetValue(CurrentPath, out var folderContents))
        {
            foreach (var item in folderContents.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name))
            {
                Items.Add(item);
            }
        }
        UpdateBreadcrumbs();
    }

    private void UpdateBreadcrumbs()
    {
        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(("Home", "/"));

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