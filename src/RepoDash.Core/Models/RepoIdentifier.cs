namespace RepoDash.Core.Models;

public readonly record struct RepoIdentifier(string Name)
{
    public override string ToString() => Name;
}
