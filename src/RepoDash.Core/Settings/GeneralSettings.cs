using System.Collections.Generic;

namespace RepoDash.Core.Settings;

public class GeneralSettings
{
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+Space";
    public string SearchShortcut { get; set; } = "Ctrl+F";
    public bool MinimizeToTray { get; set; } = true;
    public int ListItemHeight { get; set; } = 64;
    public double GroupWidth { get; set; } = 360;
    public int GroupingSegmentNumber { get; set; } = 2;
    public int AutocompleteThreshold { get; set; } = 5;
    public int AutoRefreshTopUsagePercentage { get; set; } = 25;
    public bool ShowRecentGroup { get; set; } = true;
    public bool ShowFrequentGroup { get; set; } = true;
    public int RecentLimit { get; set; } = 12;
    public int FrequentLimit { get; set; } = 12;
    public string DefaultBranch { get; set; } = "main";
    public string GitUiToolPath { get; set; } = string.Empty;
    public string ExplorerPreference { get; set; } = "TotalCommander";
    public List<string> InlineActions { get; set; } = new() { "Launch", "Browse", "OpenRemote", "OpenPipelines", "OpenGitUi" };
    public string ExtraShortcutsPlacement { get; set; } = "Top";
    public double ExtraShortcutIconSize { get; set; } = 36;
}
