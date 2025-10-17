using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RepoDash.Core.Settings;

public sealed class GroupColorOverride
{
    [Required]
    [DisplayName("Group Name")]
    [Description("Exact group name (OrdinalIgnoreCase).")]
    public string GroupName { get; set; } = string.Empty;

    [DisplayName("Color")]
    [Description("ARGB hex, e.g. #FFEFEFFF (light pastel).")]
    public string ColorCode { get; set; } = "#FFEFEFFF";
}