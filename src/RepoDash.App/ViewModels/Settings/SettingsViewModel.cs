using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Abstractions;

namespace RepoDash.App.ViewModels.Settings
{
    public partial class SettingsViewModel<TSettings> : ObservableObject
        where TSettings : class, new()
    {
        private readonly ISettingsStore<TSettings> _store;

        public string Title { get; }
        public Action? RequestClose { get; set; }

        public TSettings TypedModel => _store.Current;
        public object Model => _store.Current;

        public SettingsViewModel(ISettingsStore<TSettings> store, string title)
        {
            _store = store;
            Title = title;
        }


        [RelayCommand]
        private async Task SaveAsync()
        {
            await _store.UpdateAsync();  
            RequestClose?.Invoke();      
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _store.ReloadAsync();  
            RequestClose?.Invoke();      
        }
    }
}
