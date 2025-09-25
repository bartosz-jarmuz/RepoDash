using System.Threading;
using System.Threading.Tasks;
using RepoDash.App.State;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.App.Services.Settings;

public sealed class SettingsBootstrapper
{
    private readonly ISettingsStore<GeneralSettings> _generalStore;
    private readonly ISettingsStore<RepositoriesSettings> _repoStore;
    private readonly ISettingsStore<ShortcutsSettings> _shortcutsStore;
    private readonly ISettingsStore<ColorRulesSettings> _colorStore;
    private readonly ISettingsStore<ExternalToolSettings> _toolsStore;
    private readonly ISettingsStore<StatusPollingSettings> _statusStore;

    public SettingsBootstrapper(
        ISettingsStore<GeneralSettings> generalStore,
        ISettingsStore<RepositoriesSettings> repoStore,
        ISettingsStore<ShortcutsSettings> shortcutsStore,
        ISettingsStore<ColorRulesSettings> colorStore,
        ISettingsStore<ExternalToolSettings> toolsStore,
        ISettingsStore<StatusPollingSettings> statusStore)
    {
        _generalStore = generalStore;
        _repoStore = repoStore;
        _shortcutsStore = shortcutsStore;
        _colorStore = colorStore;
        _toolsStore = toolsStore;
        _statusStore = statusStore;
    }

    public async Task InitializeAsync(AppState state, CancellationToken cancellationToken = default)
    {
        state.GeneralSettings = await _generalStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        state.RepositoriesSettings = await _repoStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        state.ShortcutsSettings = await _shortcutsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        state.ColorRulesSettings = await _colorStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        state.ExternalToolSettings = await _toolsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        state.StatusPollingSettings = await _statusStore.LoadAsync(cancellationToken).ConfigureAwait(false);
    }
}
