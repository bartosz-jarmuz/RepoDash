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

    [Category("Shortcuts")]
    [DisplayName("Entries")]
    public ObservableCollection<ShortcutEntry> ShortcutEntries { get; init; } = new();
}