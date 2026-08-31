using System.Globalization;

namespace DiscordBot.FantasyPremierLeague.ChipWatch;

public enum FplChipWatchSelectionStatus
{
    Found,
    NotFound,
    Ambiguous
}

public sealed record FplChipWatchSelectionResult(
    FplChipWatchSelectionStatus Status,
    FplManagerChipWatch? Manager,
    IReadOnlyList<string> Candidates);

public sealed class FplChipWatchSelectionService
{
    private const int MaxCandidates = 10;

    public FplChipWatchSelectionResult Select(
        IReadOnlyList<FplManagerChipWatch> managers,
        string query)
    {
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var trimmed = query.Trim();
        var lower = trimmed.ToLower(CultureInfo.InvariantCulture);

        // Exact team name
        var exact = managers.Where(m =>
                string.Equals(m.EntryName.Trim(), trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.PlayerName.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count == 1)
        {
            return new FplChipWatchSelectionResult(FplChipWatchSelectionStatus.Found, exact[0], []);
        }

        if (exact.Count > 1)
        {
            return new FplChipWatchSelectionResult(
                FplChipWatchSelectionStatus.Ambiguous,
                null,
                exact.Take(MaxCandidates).Select(m => m.EntryName).ToList());
        }

        // Unique partial
        var partial = managers.Where(m =>
                m.EntryName.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                m.PlayerName.Contains(lower, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (partial.Count == 0)
        {
            return new FplChipWatchSelectionResult(FplChipWatchSelectionStatus.NotFound, null, []);
        }

        if (partial.Count == 1)
        {
            return new FplChipWatchSelectionResult(FplChipWatchSelectionStatus.Found, partial[0], []);
        }

        return new FplChipWatchSelectionResult(
            FplChipWatchSelectionStatus.Ambiguous,
            null,
            partial.Take(MaxCandidates).Select(m => m.EntryName).ToList());
    }
}
