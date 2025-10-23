using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RepoDash.App.Abstractions;
using RepoDash.App.Services;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.Tests.Services;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class RepoStatusRefreshServiceTests
{
    [Test]
    public async Task MarkRefreshed_ShouldPersistPerRoot()
    {
        var settings = new GeneralSettings
        {
            StatusRefreshHistory = new Dictionary<string, DateTimeOffset?>(),
            StatusRefreshCooldownMinutes = 10
        };
        var (src, store) = MakeSources(settings);
        var sut = new RepoStatusRefreshService(src, store);

        Assert.That(sut.ShouldRefresh(@"C:\root-a", force: false), Is.True);

        var stamp = await sut.MarkRefreshedAsync(@"C:\root-a");

        Assert.That(settings.StatusRefreshHistory.Count, Is.EqualTo(1));
        var key = GetSingleKey(settings.StatusRefreshHistory);
        Assert.That(settings.StatusRefreshHistory[key], Is.EqualTo(stamp));
        Assert.That(sut.GetLastRefresh(@"C:\root-a"), Is.EqualTo(stamp));
        Assert.That(sut.ShouldRefresh(@"C:\root-a", force: false), Is.False);
        Assert.That(sut.ShouldRefresh(@"D:\root-b", force: false), Is.True, "Other roots should remain refreshable");
    }

    [Test]
    public async Task ShouldRefresh_RespectsCooldown()
    {
        var settings = new GeneralSettings
        {
            StatusRefreshHistory = new Dictionary<string, DateTimeOffset?>(),
            StatusRefreshCooldownMinutes = 5
        };
        var (src, store) = MakeSources(settings);
        var sut = new RepoStatusRefreshService(src, store);
        var root = @"C:\dev\repo";

        Assert.That(sut.ShouldRefresh(root, force: false), Is.True);

        await sut.MarkRefreshedAsync(root);
        Assert.That(sut.ShouldRefresh(root, force: false), Is.False);

        settings.StatusRefreshHistory[GetSingleKey(settings.StatusRefreshHistory)] = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(6);
        Assert.That(sut.ShouldRefresh(root, force: false), Is.True);
    }

    [Test]
    public async Task ShouldRefresh_ForceBypassesCooldown()
    {
        var settings = new GeneralSettings
        {
            StatusRefreshHistory = new Dictionary<string, DateTimeOffset?>(),
            StatusRefreshCooldownMinutes = 30
        };
        var (src, store) = MakeSources(settings);
        var sut = new RepoStatusRefreshService(src, store);

        var root = @"C:\root";
        await sut.MarkRefreshedAsync(root);
        Assert.That(sut.ShouldRefresh(root, force: true), Is.True);
    }

    private static (IReadOnlySettingsSource<GeneralSettings> Source, ISettingsStore<GeneralSettings> Store)
        MakeSources(GeneralSettings settings)
    {
        var source = new StubSettingsSource(settings);
        var store = new StubSettingsStore(settings);
        return (source, store);
    }

    private static string GetSingleKey(Dictionary<string, DateTimeOffset?> map)
    {
        using var enumerator = map.Keys.GetEnumerator();
        Assert.That(enumerator.MoveNext(), Is.True);
        return enumerator.Current;
    }

    private sealed class StubSettingsSource : IReadOnlySettingsSource<GeneralSettings>
    {
        public StubSettingsSource(GeneralSettings current) => Current = current;
        public GeneralSettings Current { get; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class StubSettingsStore : ISettingsStore<GeneralSettings>
    {
        public StubSettingsStore(GeneralSettings current) => Current = current;
        public GeneralSettings Current { get; }
        public event EventHandler? SettingsChanged;
        public Task UpdateAsync(Action<GeneralSettings>? mutate = null, CancellationToken ct = default)
        {
            mutate?.Invoke(Current);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
        public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
