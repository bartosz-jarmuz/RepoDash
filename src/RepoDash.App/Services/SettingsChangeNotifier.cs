using System.ComponentModel;

namespace RepoDash.App.Services;

public sealed class SettingsChangeNotifier : INotifyPropertyChanged
{
    public static SettingsChangeNotifier Default { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Version { get; private set; } = 0;

    public void Bump()
    {
        Version++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
    }
}