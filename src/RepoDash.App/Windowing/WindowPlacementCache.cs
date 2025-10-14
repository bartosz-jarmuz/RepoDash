namespace RepoDash.App.Windowing;

public sealed class WindowPlacementCache
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;
    public string State { get; set; } = "Normal";
}