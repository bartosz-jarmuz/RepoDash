using System.Windows;
using RepoDash.App.Windowing;

namespace RepoDash.App.Behaviors;

public sealed class WindowPlacementBehavior : DependencyObject
{
    public static readonly DependencyProperty CacheProperty =
        DependencyProperty.RegisterAttached(
            "Cache",
            typeof(IWindowPlacementCache),
            typeof(WindowPlacementBehavior),
            new PropertyMetadata(null, OnAttach));

    public static void SetCache(DependencyObject element, IWindowPlacementCache? value) => element.SetValue(CacheProperty, value);
    public static IWindowPlacementCache? GetCache(DependencyObject element) => (IWindowPlacementCache?)element.GetValue(CacheProperty);

    private static void OnAttach(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window w || e.NewValue is not IWindowPlacementCache cache) return;

        // Restore BEFORE first render to avoid the visual jump
        w.SourceInitialized += (_, __) =>
        {
            var c = cache.Load();

            w.WindowStartupLocation = WindowStartupLocation.Manual;

            if (!double.IsNaN(c.Left)) w.Left = c.Left;
            if (!double.IsNaN(c.Top)) w.Top = c.Top;
            if (c.Width > 0) w.Width = c.Width;
            if (c.Height > 0) w.Height = c.Height;

            if (Enum.TryParse(c.State, out WindowState ws)) w.WindowState = ws;

            var rect = new System.Drawing.Rectangle((int)w.Left, (int)w.Top, (int)Math.Max(1, w.Width), (int)Math.Max(1, w.Height));
            var onAny = System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect));
            if (!onAny)
            {
                w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        };

        w.Closing += (_, __) =>
        {
            var c = new WindowPlacementCache
            {
                Left = w.Left,
                Top = w.Top,
                Width = w.Width,
                Height = w.Height,
                State = w.WindowState.ToString()
            };
            cache.Save(c);
        };
    }
}
