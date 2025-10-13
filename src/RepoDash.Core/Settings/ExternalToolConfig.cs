using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

namespace RepoDash.Core.Settings;

public sealed class ExternalToolConfig
{
    [Required, DisplayName("Tool Name")]
    public string Name { get; set; } = string.Empty;

    [Required, DisplayName("Script/Executable Path")]
    public string ScriptPath { get; set; } = string.Empty;

    [DisplayName("Button Color")]
    public Color ButtonColor { get; set; } = Color.LightGray;

    [DisplayName("Arguments (supports tokens)"),
     Description("Use $(SolutionName), $(SolutionPath), $(FolderName). Separate by space as usual.")]
    public string? Arguments { get; set; }
}