namespace CloudUnify.Core.Extensions;

public static class SearchExtensions {
    public static async Task<List<UnifiedCloudFile>> SearchFilesAsync(
        this CloudUnify cloudUnify,
        string searchTerm,
        SearchOptions? options = null) {
        options ??= new SearchOptions();

        var allFiles = await cloudUnify.ListAllFilesAsync(options.Path);

        var filteredFiles = allFiles.Where(file => {
            var matchesSearchTerm = string.IsNullOrEmpty(searchTerm) ||
                                    file.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);

            var matchesFileType = options.FileTypes == null ||
                                  !options.FileTypes.Any() ||
                                  options.FileTypes.Any(ft =>
                                      file.MimeType.Contains(ft, StringComparison.OrdinalIgnoreCase));

            var matchesDateRange = (!options.StartDate.HasValue || file.ModifiedAt >= options.StartDate.Value) &&
                                   (!options.EndDate.HasValue || file.ModifiedAt <= options.EndDate.Value);

            var matchesIsFolder = !options.FoldersOnly || file.IsFolder;
            var matchesIsFile = !options.FilesOnly || !file.IsFolder;

            return matchesSearchTerm && matchesFileType && matchesDateRange && matchesIsFolder && matchesIsFile;
        });

        // Apply sorting
        if (options.SortBy != null)
            filteredFiles = options.SortDirection == SortDirection.Ascending
                ? filteredFiles.OrderBy(f => GetPropertyValue(f, options.SortBy))
                : filteredFiles.OrderByDescending(f => GetPropertyValue(f, options.SortBy));

        return filteredFiles.ToList();
    }

    private static object GetPropertyValue(UnifiedCloudFile file, string propertyName) {
        return propertyName.ToLowerInvariant() switch {
            "name" => file.Name,
            "size" => file.Size,
            "createdat" => file.CreatedAt,
            "modifiedat" => file.ModifiedAt,
            _ => file.Name
        };
    }
}

public class SearchOptions {
    public string Path { get; set; } = "/";
    public List<string> FileTypes { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool FoldersOnly { get; set; }
    public bool FilesOnly { get; set; }
    public string? SortBy { get; set; } = null;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
}

public enum SortDirection {
    Ascending,
    Descending
}