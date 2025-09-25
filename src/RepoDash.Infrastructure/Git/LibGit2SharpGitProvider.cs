using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;

namespace RepoDash.Infrastructure.Git;

public sealed class LibGit2SharpGitProvider : IGitProvider
{
    public Task<RepoStatus> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var repo = new Repository(repositoryPath);
            var branch = repo.Head;
            var status = repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true });
            var relation = DetermineRelation(branch);
            var isDirty = status.IsDirty;
            return new RepoStatus(branch.FriendlyName, relation, isDirty, DateTimeOffset.UtcNow);
        }, cancellationToken);
    }

    public Task<string?> GetRemoteUrlAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var repo = new Repository(repositoryPath);
            return repo.Network?.Remotes["origin"]?.Url;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetLocalBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<string>>(() =>
        {
            using var repo = new Repository(repositoryPath);
            return repo.Branches.Where(b => !b.IsRemote).Select(b => b.FriendlyName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }, cancellationToken);
    }

    public Task CheckoutAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var repo = new Repository(repositoryPath);
            Commands.Checkout(repo, branchName);
        }, cancellationToken);
    }

    public Task FetchAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var repo = new Repository(repositoryPath);
            var remote = repo.Network.Remotes["origin"];
            Commands.Fetch(repo, remote.Name, remote.FetchRefSpecs.Select(x => x.Specification), null, null);
        }, cancellationToken);
    }

    public Task PullAsync(string repositoryPath, bool rebase, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var repo = new Repository(repositoryPath);
            var signature = repo.Config.BuildSignature(DateTimeOffset.UtcNow);
            var options = new PullOptions
            {
                MergeOptions = new MergeOptions
                {
                    FastForwardStrategy = rebase ? FastForwardStrategy.FastForwardOnly : FastForwardStrategy.Default
                }
            };

            Commands.Pull(repo, signature, options);
        }, cancellationToken);
    }

    private static RepoBranchRelation DetermineRelation(Branch branch)
    {
        if (!branch.IsTracking)
        {
            return RepoBranchRelation.Unknown;
        }

        var ahead = branch.TrackingDetails.AheadBy ?? 0;
        var behind = branch.TrackingDetails.BehindBy ?? 0;

        if (ahead > 0 && behind > 0)
        {
            return RepoBranchRelation.Diverged;
        }

        if (ahead > 0)
        {
            return RepoBranchRelation.Ahead;
        }

        if (behind > 0)
        {
            return RepoBranchRelation.Behind;
        }

        return RepoBranchRelation.UpToDate;
    }
}
