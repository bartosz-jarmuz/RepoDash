using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;

namespace RepoDash.Core.Settings;

public sealed class ToolsPanelSettings
{
    [Category("Panel")]
    [DisplayName("Show panel")]
    public bool ShowPanel { get; set; } = true;

    [Category("Panel")]
    [DisplayName("Placement")]
    public ToolsPanelPlacement Placement { get; set; } = ToolsPanelPlacement.Left;

    [Category("Panel")]
    [DisplayName("Panel width")]
    [Description("Stored width for the tools panel when docked vertically.")]
    public double PanelWidth { get; set; } = 320;

    [Category("Tools")]
    [DisplayName("Show Recent list")]
    public bool ShowRecent { get; set; } = true;

    [Category("Tools")]
    [DisplayName("Recent list height")]
    [Description("Number of rows shown for the Recent list.")]
    public int RecentListVisibleCount { get; set; } = 6;

    [Category("Tools")]
    [DisplayName("Recent items limit")]
    public int RecentItemsLimit { get; set; } = 10;

    [Category("Tools")]
    [DisplayName("Show Frequent list")]
    public bool ShowFrequent { get; set; } = true;

    [Category("Tools")]
    [DisplayName("Frequent list height")]
    [Description("Number of rows shown for the Frequent list.")]
    public int FrequentListVisibleCount { get; set; } = 6;

    [Category("Tools")]
    [DisplayName("Frequent items limit")]
    public int FrequentItemsLimit { get; set; } = 10;

    [Category("Tools")]
    [DisplayName("Pins apply to Recent")]
    public bool PinningAppliesToRecent { get; set; } = true;

    [Category("Tools")]
    [DisplayName("Pins apply to Frequent")]
    public bool PinningAppliesToFrequent { get; set; } = true;

    [Category("Tools")]
    [DisplayName("Entries")]
    public ObservableCollection<ToolsPanelEntry> Entries { get; init; } = new();

    public ToolsPanelSettings()
    {
        EnsureDefaults();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
        => EnsureDefaults();

    private void EnsureDefaults()
    {
        if (!Entries.Any(e => string.Equals(e.Key, SpecialGroupKeys.Recent, StringComparison.OrdinalIgnoreCase)))
        {
            Entries.Add(new ToolsPanelEntry { Key = SpecialGroupKeys.Recent });
        }

        if (!Entries.Any(e => string.Equals(e.Key, SpecialGroupKeys.Frequent, StringComparison.OrdinalIgnoreCase)))
        {
            Entries.Add(new ToolsPanelEntry { Key = SpecialGroupKeys.Frequent });
        }
    }
}
