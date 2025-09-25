using System.Collections.Generic;
using RepoDash.Core.Models;

namespace RepoDash.Core.Settings;

public class ColorRulesSettings
{
    public List<ColorRuleGroup> Groups { get; set; } = new();
    public ColorSwatch DefaultSwatch { get; set; } = new(0x4C, 0xAF, 0x50);
}

public class ColorRuleGroup
{
    public string Name { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public string? HexColor { get; set; }
}
