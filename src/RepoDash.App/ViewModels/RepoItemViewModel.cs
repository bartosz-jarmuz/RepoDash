using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using RepoDash.Core.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace RepoDash.App.ViewModels;

public partial class RepoItemViewModel : ObservableObject
{
    [ObservableProperty]
    private RepoSnapshot _snapshot;

    [ObservableProperty]
    private MediaBrush _branchBrush = MediaBrushes.Gray;

    [ObservableProperty]
    private string _branchText = string.Empty;

    [ObservableProperty]
    private string _accentColor = "#4C4F56";

    public RepoItemViewModel(RepoSnapshot snapshot)
    {
        Snapshot = snapshot;
        UpdateBranchState();
    }

    public string DisplayName => Snapshot.Descriptor.HasSolution && Snapshot.Descriptor.SolutionPath is { } sln
        ? Path.GetFileNameWithoutExtension(sln) ?? Snapshot.Identifier.Name
        : Snapshot.Identifier.Name;

    public string RepositoryPath => Snapshot.Descriptor.RepositoryPath;

    partial void OnSnapshotChanged(RepoSnapshot value)
    {
        UpdateBranchState();
    }

    public void ApplyAccent(string hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            AccentColor = hex;
        }
    }

    private void UpdateBranchState()
    {
        var status = Snapshot.Status;
        var branch = status.BranchName ?? "unknown";
        if (status.IsDirty && !branch.EndsWith("*", StringComparison.Ordinal))
        {
            branch += "*";
        }

        BranchText = branch;
        BranchBrush = status.Relation switch
        {
            RepoBranchRelation.UpToDate => MediaBrushes.LightGreen,
            RepoBranchRelation.Ahead => MediaBrushes.DeepSkyBlue,
            RepoBranchRelation.Behind => MediaBrushes.Orange,
            RepoBranchRelation.Diverged => MediaBrushes.IndianRed,
            _ => MediaBrushes.Gray
        };

        OnPropertyChanged(nameof(DisplayName));
    }
}
