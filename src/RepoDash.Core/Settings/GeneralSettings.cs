using System.ComponentModel;

namespace RepoDash.Core.Settings;

public sealed class GeneralSettings
{
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+Space";
    public bool CloseToTray { get; set; } = true;
    public int ListItemVisibleCount { get; set; } = 10;
    public int GroupPanelWidth { get; set; } = 360;
    public int GroupingSegment { get; set; } = 2;
    public int AutoCompleteWhenAtMost { get; set; } = 5;
    public int StatusPollIntervalSeconds { get; set; } = 120;
    public int StatusPollTopPercent { get; set; } = 20;
    public bool StatusOnDemandOnly { get; set; } = false;
    public string RepoRoot { get; set; } = @"C:\dev\git";
    public string? TotalCommanderPath { get; set; }
    public string? GitUiPath { get; set; }
    public bool ShowRecent { get; set; } = true;
    public bool ShowFrequent { get; set; } = true;

    [DisplayName("Remote Pipelines Url Part")]
    [Description(
        "Relative path appended to a repository page URL to open its pipelines/runs list" +
        "Use \"/-/pipelines\" for GitLab (default). Use \"/actions\" for GitHub Actions.")]
    public string RemotePipelinesUrlPart { get; set; } = "/-/pipelines";
}