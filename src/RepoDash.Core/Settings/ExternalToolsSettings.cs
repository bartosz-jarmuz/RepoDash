namespace RepoDash.Core.Settings;

public sealed class ExternalToolsSettings
{
    public List<ExternalToolConfig> ExternalToolConfigs { get; set; } = new();
}