namespace RepoDash.App.Windowing;

public interface IWindowPlacementCache
{
    WindowPlacementCache Load();
    void Save(WindowPlacementCache cache);
}