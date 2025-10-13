using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RepoDash.Core.Settings;

public sealed class RepositoriesSettings
{
    [Category("Filtering"), DisplayName("Excluded (path contains)"),
     Description("Repositories whose full path contains any of these substrings will be excluded.")]
    public ObservableCollection<string> ExcludedPathParts { get; init; } = new();

    [Category("Categorization"), DisplayName("Category Overrides"),
     Description("Map of 'match substring(s)' → 'category name'. Repos whose path or name contains any substring are placed in the target category.")]
    public ObservableCollection<CategoryOverride> CategoryOverrides { get; init; } = new();
}