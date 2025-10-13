using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RepoDash.Core.Settings;

public sealed class ColorRule
{
    [DisplayName("Color"), Description("Brush color applied to matching repo names/paths.")]
    public string ColorCode { get; set; } = "#4682B4";

    [Required, DisplayName("Keywords"),
     Description("Keywords to match (OrdinalIgnoreCase). For grouped rules, hash is computed from the first string.")]
    public ObservableCollection<string> Keywords { get; init; } = new();
}