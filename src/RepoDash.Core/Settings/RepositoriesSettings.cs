using System.Collections.Generic;
using RepoDash.Core.Models;

namespace RepoDash.Core.Settings;

public class RepositoriesSettings
{
    public string RepoRoot { get; set; } = string.Empty;
    public List<string> ExcludedFragments { get; set; } = new();
    public List<CategoryOverride> CategoryOverrides { get; set; } = new();
}
