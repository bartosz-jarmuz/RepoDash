using System.ComponentModel;

namespace RepoDash.Core.Settings;

public sealed class GeneralSettings
{
    [Category("1. General Behavior")]
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+Space";
    
    [Category("1. General Behavior")]
    public bool CloseToTray { get; set; } = true;

    [Category("2. User interface")]
    public int ListItemVisibleCount { get; set; } = 10;

    [Category("2. User interface")]
    public int GroupPanelWidth { get; set; } = 360;
    
    [Category("3. Repository")]
    public int GroupingSegment { get; set; } = 2;
    
    [Category("3. Repository")]
    public string RepoRoot { get; set; } = @"C:\dev\git";

    [Category("4. Additional tools")]
    [Description(
        "Provide path to TotalCommander to use it instead of default file explorer.")]
    public string? TotalCommanderPath { get; set; }
    
    [Category("4. Additional tools")]
    [DisplayName("Git UI tool path")]
    public string? GitUiPath { get; set; }
    
    [Category("4. Additional tools")]
    [DisplayName("Git command line tool path")]
    public string? GitCliPath { get; set; }

    [Category("4. Additional tools")]
    [DisplayName("Non-solution repo editor path")]
    [Description(
        "Repositories which don't contain an SLN will be opened with this tool." +
        "Leave empty to open in file explorer")]
    public string? NonSlnRepoEditorPath { get; set; }

    [Category("5. Remote")]
    [DisplayName("Remote Pipelines Url Part")]
    [Description(
        "Relative path appended to a repository page URL to open its pipelines/runs list" +
        "Use \"/-/pipelines\" for GitLab (default). Use \"/actions\" for GitHub Actions.")]
    public string RemotePipelinesUrlPart { get; set; } = "/-/pipelines";

    [Category("6. Recent and frequently used repos")]
    public bool ShowRecent { get; set; } = true;

    [Category("6. Recent and frequently used repos")]
    [DisplayName("Recent items limit")]
    public int RecentItemsLimit { get; set; } = 10;

    [Category("6. Recent and frequently used repos")]
    public bool ShowFrequent { get; set; } = true;

    [Category("6. Recent and frequently used repos")]
    [DisplayName("Frequent items limit")]
    public int FrequentItemsLimit { get; set; } = 10;

}
