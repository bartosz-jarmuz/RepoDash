using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.Tests.ViewModels;

[TestFixture]
public sealed class RepoGroupViewModelTests
{
    [Test]
    public async Task ToggleVisibilityCommand_TogglesRecentSetting()
    {
        var general = new GeneralSettings { ShowRecent = true };
        var generalSource = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        generalSource.SetupGet(s => s.Current).Returns(general);

        var generalStore = new Mock<ISettingsStore<GeneralSettings>>();
        generalStore.SetupGet(s => s.Current).Returns(general);
        generalStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<GeneralSettings>>(), It.IsAny<CancellationToken>()))
            .Returns((Action<GeneralSettings>? mutate, CancellationToken _) =>
            {
                mutate?.Invoke(general);
                generalStore.Raise(s => s.SettingsChanged += null!, EventArgs.Empty);
                return Task.CompletedTask;
            });

        var colorSettings = new ColorSettings();
        var colorStore = new Mock<ISettingsStore<ColorSettings>>();
        colorStore.SetupGet(s => s.Current).Returns(colorSettings);
        colorStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<ColorSettings>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vm = new RepoGroupViewModel(generalSource.Object, generalStore.Object, colorStore.Object)
        {
            InternalKey = "__special_recent",
            GroupKey = "Recent",
            IsSpecial = true
        };

        Assert.That(vm.ToggleVisibilityCommand.CanExecute(null), Is.True);

        await vm.ToggleVisibilityCommand.ExecuteAsync(null);

        Assert.That(general.ShowRecent, Is.False);
        Assert.That(vm.ToggleVisibilityLabel, Does.StartWith("Show"));
    }
}
