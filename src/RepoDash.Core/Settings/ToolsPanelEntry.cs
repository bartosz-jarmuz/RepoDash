using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RepoDash.Core.Settings;

public sealed class ToolsPanelEntry
{
    [Required]
    [DisplayName("Key")]
    [Description("Internal key that identifies the tool (e.g. recent, frequent).")]
    public string Key { get; set; } = string.Empty;

    [DisplayName("Display Name")]
    [Description("Optional custom name shown in the UI when supported.")]
    public string? DisplayName { get; set; }
}

