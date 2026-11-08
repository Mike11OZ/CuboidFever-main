using System.Collections.Generic;
using System.Linq;

namespace CuboidFever;

// The warning shown when a world is opened that uses blocks this version of the game does not have.
public static class MissingBlocksMessage
{
    // Null when nothing is missing. Otherwise names the first few blocks and counts the rest.
    public static string Make(IReadOnlyList<string> missing, int maxNames = 3)
    {
        if (missing == null || missing.Count == 0) return null;

        string names = string.Join(", ", missing.Take(maxNames));
        int more = missing.Count - maxNames;
        if (more > 0) names += $" and {more} more";

        return $"This world uses blocks this version does not have, so they now show as air: {names}.";
    }
}
