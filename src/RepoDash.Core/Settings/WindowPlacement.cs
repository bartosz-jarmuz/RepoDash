namespace RepoDash.Core.Settings;

public sealed class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string WindowState { get; set; } = "Normal";
    public string? ScreenDeviceName { get; set; }
}