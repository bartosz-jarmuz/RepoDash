# RepoDash User Guide (Draft)

> This document will be expanded as features are implemented. It currently outlines the primary concepts, configuration locations, and planned workflows.

## Overview

RepoDash is a productivity dashboard for developers working with many Git repositories. It offers quick launch actions, search, grouping, usage tracking, and configurable integrations for Git workflows, external tools, and shortcuts.

## Key Features (Future Roadmap)

- Fast search with keyboard-first navigation and autocomplete.
- Cached repository discovery for multiple roots with background refresh.
- Configurable inline actions (launch, browse, remote URLs, pipelines, Git UI).
- Usage tracking driving recent/frequent lists and selective status polling.
- JSON-based settings for general behaviour, repositories, shortcuts, colors, and tools.
- Global hotkey and system tray integration for quick access.
- Pluggable colorization, Git providers, and remote URL builders.

## Configuration Files

All settings and caches are stored under `%USERPROFILE%\Documents\RepoDash`:

- `Settings/settings.general.json`
- `Settings/settings.repositories.json`
- `Settings/settings.shortcuts.json`
- `Settings/settings.colors.json`
- `Settings/settings.tools.json`
- `Settings/settings.status.json`
- `Cache/cache.<normalized-root>.json`
- `Usage/usage.json`

## Getting Started

1. Set your repository root path in the top bar. RepoDash will load cached data immediately (if available) and refresh in the background.
2. Use the search box to filter repositories. Suggestions appear automatically when only a few matches remain.
3. Adjust list height with the numeric control to fit your workflow.
4. Launch a repository with the rocket icon, or right-click (coming soon) for additional actions.

## Fake Repository Tree

Use the script in `docs/FakeRepos/generate-fake-repos.ps1` to create a demo repository structure for presentations or testing.

```powershell
.\docsakeReposuild-demo.ps1 -RootPath C:	emp
epo-sandbox
```

## Open Source Attribution (Initial)

- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp)

Full OSS attribution and licensing sections will be added alongside third-party usage.

---

_This is a placeholder guide; sections on shortcuts, color rules, external tools, and Git operations will be detailed as those modules are implemented._
