namespace RepoDash.Core.Settings;

using System.Collections.ObjectModel;
using System.ComponentModel;

public sealed class ShortcutsSettings
{
    [Category("Panel")]
    [DisplayName("Placement")]
    public ShortcutsPanelPlacement Placement { get; set; } = ShortcutsPanelPlacement.Left;

    [Category("Panel")]
    [DisplayName("Item Size")]
    public ShortcutsItemSize ItemSize { get; set; } = ShortcutsItemSize.Medium;

    [Category("Panel")]
    [DisplayName("Panel width")]
    [Description("Stored width for the shortcuts panel when docked vertically.")]
    public double PanelWidth { get; set; } = 148;

    [Category("Shortcuts")]
    [DisplayName("Entries")]
    public ObservableCollection<ShortcutEntry> ShortcutEntries { get; init; } = new();
}
