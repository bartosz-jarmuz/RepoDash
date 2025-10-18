namespace RepoDash.Core.Settings;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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

    [Category("1. Group headers")]
    [DisplayName("Group color overrides")]
    [Description("Optional per-group header colors. If set for a group, it overrides the auto pastel. Case-insensitive by GroupName.")]
    public ObservableCollection<GroupColorOverride> GroupColorOverrides { get; init; } = new();

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

    [Category("4. Automatic color tuning")]
    [DisplayName("Automatic color opacity (%)")]
    [Description("How transparent the automatic group colors should be. 30-50% gives a pastel overlay.")]
    [Range(0, 100)]
    public int AutomaticGroupOpacityPercent { get; set; } = 40;

    [Category("4. Automatic color tuning")]
    [DisplayName("Background color lightness (%)")]
    [Description("Controls how much the automatic background colors are brightened. 0 = none, 100 = brightest.")]
    [Range(0, 100)]
    public int BackgroundLightnessPercent { get; set; } = 50;

    [Category("4. Automatic color tuning")]
    [DisplayName("Foreground color darkness (%)")]
    [Description("Controls how much the automatic foreground colors are darkened. 0 = none, 100 = darkest.")]
    [Range(0, 100)]
    public int ForegroundDarknessPercent { get; set; } = 50;
}
