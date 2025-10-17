namespace RepoDash.Core.Settings;

using System.Collections.ObjectModel;
using System.ComponentModel;

public sealed class ColorSettings
{
   

    [Category("1. Group headers")]
    [DisplayName("Color Recent group header")]
    [Description("If true, the 'Recent' group header shows a colored background.")]
    public bool AddColorToRecentGroupBox { get; set; } = true;

    [Category("1. Group headers")]
    [DisplayName("Color Frequent group header")]
    [Description("If true, the 'Frequently Used' group header shows a colored background.")]
    public bool AddColorToFrequentGroupBox { get; set; } = true;

    [Category("1. Group headers")]
    [DisplayName("Color automatic group headers")]
    [Description("If true, automatically generated group headers receive a light pastel color (overridable per group).")]
    public bool AddColorToAutomaticGroupBoxes { get; set; } = true;

    // === New: Concrete colors for special groups ===
    [Category("2. Special group colors")]
    [DisplayName("Recent group color")]
    [Description("ARGB hex, e.g. #FFD6E8FF (light blue).")]
    public string RecentGroupColor { get; set; } = "#FFD6E8FF"; // light blue(ish)

    [Category("2. Special group colors")]
    [DisplayName("Frequent group color")]
    [Description("ARGB hex, e.g. #FFD6F5D6 (light green).")]
    public string FrequentGroupColor { get; set; } = "#FFD6F5D6"; // light green(ish)

    [Category("3. Keyword-based repo coloring")]
    [DisplayName("Color Rules")]
    [Description("Rules that colorize repo texts when any of the keywords match (OrdinalIgnoreCase).")]
    public List<ColorRule> ColorRules { get; set; } = new();

    [Category("4. Group headers color overriding")]
    [DisplayName("Group color overrides")]
    [Description("Optional per-group header colors. If set for a group, it overrides the auto pastel. Case-insensitive by GroupName.")]
    public ObservableCollection<GroupColorOverride> GroupColorOverrides { get; init; } = new();
}