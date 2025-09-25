namespace RepoDash.Core.Models;

public enum RepoActionKind
{
    Launch,
    OpenFolder,
    OpenRemote,
    OpenPipelines,
    OpenGitUi,
    Pull,
    Fetch,
    Checkout,
    CopyName,
    CopyPath,
    ExternalTool
}

public sealed record RepoActionDescriptor(
    string Id,
    RepoActionKind Kind,
    string DisplayName,
    string IconKey,
    string? ToolTip,
    bool IsDefault,
    bool ShowInline)
{
    public static RepoActionDescriptor CreateDefaultLaunch() => new(
        "launch",
        RepoActionKind.Launch,
        "Launch",
        "IconLaunch",
        "Launch solution",
        true,
        true);
}
