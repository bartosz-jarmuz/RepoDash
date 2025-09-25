using System;
using System.Collections.Generic;

namespace RepoDash.Core.Settings;

public class ExternalToolSettings
{
    public List<ExternalToolEntry> Tools { get; set; } = new();
}

public class ExternalToolEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ScriptPath { get; set; } = string.Empty;
    public string ButtonColorHex { get; set; } = "#3C7EDB";
    public List<string> Arguments { get; set; } = new();
}
