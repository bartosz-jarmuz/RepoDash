using System.ComponentModel;

namespace RepoDash.App.Abstractions;

public interface IReadOnlySettingsSource<TSettings> : INotifyPropertyChanged
{
    TSettings Current { get; }
}