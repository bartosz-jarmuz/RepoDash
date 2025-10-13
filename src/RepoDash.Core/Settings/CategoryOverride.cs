using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RepoDash.Core.Settings;

public sealed class CategoryOverride
{
    [DisplayName("Category Name")]
    public string Category { get; set; } = string.Empty;

    [DisplayName("Match Substrings"), Description("One or more substrings. If any match, the repo goes into this category.")]
    public ObservableCollection<string> Matches { get; init; } = new();
}