using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RepoDash.Core.Settings;

public sealed class ShortcutEntry
{
    [DisplayName("Display Name"), Description("Shown in the UI. If empty, file name is used.")]
    public string? DisplayName { get; set; }

    [Required, DisplayName("Target Path or URL"),
     Description("Path to EXE/BAT/PS1/document, or URL.")]
    public string Target { get; set; } = string.Empty;

    [DisplayName("Arguments"), Description("Optional CLI arguments.")]
    public string? Arguments { get; set; }

    [DisplayName("Icon Path"), Description("Optional custom icon path. If empty, the app tries to fetch one automatically.")]
    public string? IconPath { get; set; }
}