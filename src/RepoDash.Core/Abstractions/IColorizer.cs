namespace RepoDash.Core.Abstractions;

public interface IColorizer
{
    uint? GetColorFor(string key);
}
