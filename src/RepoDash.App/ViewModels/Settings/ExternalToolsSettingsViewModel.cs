using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels.Settings;

public partial class ExternalToolsSettingsViewModel : ObservableObject
{
    public ObservableCollection<ExternalToolConfig> Tools { get; }

    public Action? OnSave { get; set; }
    public Action? OnCancel { get; set; }

    public ExternalToolsSettingsViewModel(IEnumerable<ExternalToolConfig> model) => Tools = new ObservableCollection<ExternalToolConfig>(model);

    [RelayCommand] private void Save() => OnSave?.Invoke();
    [RelayCommand] private void Cancel() => OnCancel?.Invoke();
}