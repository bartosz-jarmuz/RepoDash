using System;

namespace RepoDash.App.ViewModels;

public readonly record struct RepoStatusRefreshResult(bool Success, Exception? Error);
