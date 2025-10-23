using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
        var general = new GeneralSettings();
        var generalSource = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        generalSource.SetupGet(s => s.Current).Returns(general);

        var generalStore = new Mock<ISettingsStore<GeneralSettings>>();
        generalStore.SetupGet(s => s.Current).Returns(general);
        generalStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<GeneralSettings>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tools = new ToolsPanelSettings { ShowRecent = true };
        var toolsSource = new Mock<IReadOnlySettingsSource<ToolsPanelSettings>>();
        toolsSource.SetupGet(s => s.Current).Returns(tools);

        var toolsStore = new Mock<ISettingsStore<ToolsPanelSettings>>();
        toolsStore.SetupGet(s => s.Current).Returns(tools);
        toolsStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<ToolsPanelSettings>>(), It.IsAny<CancellationToken>()))
            .Returns((Action<ToolsPanelSettings>? mutate, CancellationToken _) =>
            {
                mutate?.Invoke(tools);
                toolsStore.Raise(s => s.SettingsChanged += null!, EventArgs.Empty);
                return Task.CompletedTask;
            });

        var colorSettings = new ColorSettings();
        var colorStore = new Mock<ISettingsStore<ColorSettings>>();
        colorStore.SetupGet(s => s.Current).Returns(colorSettings);
        colorStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<ColorSettings>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vm = new RepoGroupViewModel(generalSource.Object, generalStore.Object, toolsSource.Object, toolsStore.Object, colorStore.Object)
        {
            InternalKey = "__special_recent",
            GroupKey = "Recent",
            IsSpecial = true
        };

        Assert.That(vm.ToggleVisibilityCommand.CanExecute(null), Is.True);

        await vm.ToggleVisibilityCommand.ExecuteAsync(null);

        Assert.That(tools.ShowRecent, Is.False);
        Assert.That(vm.ToggleVisibilityLabel, Does.StartWith("Show"));
    }

    [Test]
    public void RaisesVisibleItemCountWhenGeneralListHeightChanges()
    {
        var general = new GeneralSettings { ListItemVisibleCount = 3 };
        var tools = new ToolsPanelSettings();

        var generalSource = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        generalSource.SetupGet(s => s.Current).Returns(general);

        var generalStore = new Mock<ISettingsStore<GeneralSettings>>();
        generalStore.SetupGet(s => s.Current).Returns(general);

        var toolsSource = new Mock<IReadOnlySettingsSource<ToolsPanelSettings>>();
        toolsSource.SetupGet(s => s.Current).Returns(tools);

        var toolsStore = new Mock<ISettingsStore<ToolsPanelSettings>>();
        toolsStore.SetupGet(s => s.Current).Returns(tools);

        var colorSettings = new ColorSettings();
        var colorStore = new Mock<ISettingsStore<ColorSettings>>();
        colorStore.SetupGet(s => s.Current).Returns(colorSettings);

        var vm = new RepoGroupViewModel(generalSource.Object, generalStore.Object, toolsSource.Object, toolsStore.Object, colorStore.Object);

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        general.ListItemVisibleCount = 8;
        generalSource.Raise(s => s.PropertyChanged += null!, new PropertyChangedEventArgs(nameof(IReadOnlySettingsSource<GeneralSettings>.Current)));

        CollectionAssert.Contains(raised, nameof(RepoGroupViewModel.VisibleItemCount));
        CollectionAssert.Contains(raised, nameof(RepoGroupViewModel.PanelWidth));
    }

    [Test]
    public void RaisesVisibleItemCountWhenToolsListHeightChanges()
    {
        var general = new GeneralSettings();
        var tools = new ToolsPanelSettings
        {
            RecentListVisibleCount = 2,
            FrequentListVisibleCount = 4
        };

        var generalSource = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        generalSource.SetupGet(s => s.Current).Returns(general);

        var generalStore = new Mock<ISettingsStore<GeneralSettings>>();
        generalStore.SetupGet(s => s.Current).Returns(general);

        var toolsSource = new Mock<IReadOnlySettingsSource<ToolsPanelSettings>>();
        toolsSource.SetupGet(s => s.Current).Returns(tools);

        var toolsStore = new Mock<ISettingsStore<ToolsPanelSettings>>();
        toolsStore.SetupGet(s => s.Current).Returns(tools);

        var colorSettings = new ColorSettings();
        var colorStore = new Mock<ISettingsStore<ColorSettings>>();
        colorStore.SetupGet(s => s.Current).Returns(colorSettings);

        var vm = new RepoGroupViewModel(generalSource.Object, generalStore.Object, toolsSource.Object, toolsStore.Object, colorStore.Object)
        {
            InternalKey = "__special_recent",
            IsSpecial = true
        };

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        tools.RecentListVisibleCount = 10;
        toolsSource.Raise(s => s.PropertyChanged += null!, new PropertyChangedEventArgs(nameof(IReadOnlySettingsSource<ToolsPanelSettings>.Current)));

        CollectionAssert.Contains(raised, nameof(RepoGroupViewModel.VisibleItemCount));
    }
}
