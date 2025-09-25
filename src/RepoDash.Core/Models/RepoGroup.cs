using System.Collections.Generic;

namespace RepoDash.Core.Models;

public sealed record RepoGroup(string Header, IReadOnlyList<RepoSnapshot> Items, double WidthHint);
