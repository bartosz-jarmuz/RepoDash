using System.Collections.Generic;

namespace RepoDash.Core.Settings;

public class ShortcutsSettings
{
    public List<ShortcutEntry> Shortcuts { get; set; } = new();
}

public class ShortcutEntry
{
    public string Path { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? IconCacheKey { get; set; }
}
