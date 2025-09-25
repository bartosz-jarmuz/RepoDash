using System;
using System.Collections.Generic;

namespace RepoDash.Core.Models;

public sealed record CategoryOverride(string TargetCategory, IReadOnlyList<string> MatchFragments)
{
    public bool Matches(string input)
    {
        foreach (var fragment in MatchFragments)
        {
            if (input.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
