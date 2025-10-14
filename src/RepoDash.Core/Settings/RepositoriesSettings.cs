using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RepoDash.Core.Settings;

public sealed class RepositoriesSettings
{
    [Category("Filtering")]
    [DisplayName("Excluded (path contains)")]
    [Description(
        "A list of path fragments. If a repository's full folder or solution path contains" +
        "any fragment (Ignore Case), the repository is hidden from all lists." +
        "Examples: \"archive\\\", \"\\deprecated\\\", \"samples\\playground\"")]
    public ObservableCollection<string> ExcludedPathParts { get; init; } = new();

    [Category("Categorization")]
    [DisplayName("Category Overrides")]
    [Description(
        "Force selected repositories into a specific category. " +
        "A rule applies when ANY fragment appears in the repository path, solution path or solution file name. " +
        "First matching rule wins. Matching is case-insensitive.")]
    public ObservableCollection<CategoryOverride> CategoryOverrides { get; init; } = new();
}