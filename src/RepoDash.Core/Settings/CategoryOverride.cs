using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RepoDash.Core.Settings;

public sealed class CategoryOverride
{
    [DisplayName("Category Name")]
    [Description("Category name to assign when a match occurs.")]
    public string Category { get; set; } = string.Empty;

    [DisplayName("Match Substrings")]
    [Description(
        "One or more case-insensitive fragments. " +
        "If ANY fragment is found in the repo path, solution path or solution file name, this override applies. " +
        "Enter one fragment per line.")]
    public ObservableCollection<string> Matches { get; init; } = new();
}