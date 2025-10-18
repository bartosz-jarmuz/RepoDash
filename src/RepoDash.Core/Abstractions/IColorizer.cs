namespace RepoDash.Core.Abstractions;

public interface IColorizer
{
    uint? GetBackgroundColorFor(string key);
    uint? GetForegroundColorFor(string key);
}
