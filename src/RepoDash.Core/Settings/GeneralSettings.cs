using System.ComponentModel;

namespace RepoDash.Core.Settings;

public sealed class GeneralSettings
{
    [Category("General Behavior")]
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+Space";
    
    [Category("General Behavior")]
    public bool CloseToTray { get; set; } = true;

    [Category("User interface")]
    public int ListItemVisibleCount { get; set; } = 10;

    [Category("User interface")]
    public int GroupPanelWidth { get; set; } = 360;
    
    [Category("Repository")]
    public int GroupingSegment { get; set; } = 2;
    
    [Category("Repository")]
    public string RepoRoot { get; set; } = @"C:\dev\git";

    [Category("Additional tools")]
    public string? TotalCommanderPath { get; set; }
    
    [Category("Additional tools")]
    [DisplayName("Git UI tool path")]
    public string? GitUiPath { get; set; }
    
    [Category("Additional tools")]
    [DisplayName("Git command line tool path")]
    public string? GitCliPath { get; set; }


    [Category("Remote")]
    [DisplayName("Remote Pipelines Url Part")]
    [Description(
        "Relative path appended to a repository page URL to open its pipelines/runs list" +
        "Use \"/-/pipelines\" for GitLab (default). Use \"/actions\" for GitHub Actions.")]
    public string RemotePipelinesUrlPart { get; set; } = "/-/pipelines";

    public int AutoCompleteWhenAtMost { get; set; } = 5;
    public int StatusPollIntervalSeconds { get; set; } = 120;
    public int StatusPollTopPercent { get; set; } = 20;
    public bool StatusOnDemandOnly { get; set; } = false;
    public bool ShowRecent { get; set; } = true;
    public bool ShowFrequent { get; set; } = true;

  
}