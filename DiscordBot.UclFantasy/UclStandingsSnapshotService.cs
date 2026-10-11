using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBot.UclFantasy;

/// <summary>
/// Computes a stable, deterministic fingerprint of the meaningful UCL league
/// standings. The fingerprint captures league/phase/matchday identity,
/// stable member identities, team/manager names, ranks, total and matchday
/// points and rank-change metadata that materially affects the published
/// Discord post. Source timestamps, buster nonces, response ordering,
/// formatting and message wording are excluded by construction.
/// </summary>
public sealed class UclStandingsSnapshotService
{
    public string ComputeFingerprint(UclLeagueStandings standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var rows = standings.Members
            .Select(member => new SnapshotRow(
                member.StableId,
                member.TeamName ?? string.Empty,
                member.ManagerName ?? string.Empty,
                member.Rank,
                member.LastRank,
                member.TotalPoints,
                member.MatchdayPoints))
            .ToArray();

        Array.Sort(rows, static (left, right) =>
        {
            var id = left.StableId.CompareTo(right.StableId);
            if (id != 0)
            {
                return id;
            }

            var team = string.Compare(
                left.TeamName,
                right.TeamName,
                StringComparison.Ordinal);
            if (team != 0)
            {
                return team;
            }

            var manager = string.Compare(
                left.ManagerName,
                right.ManagerName,
                StringComparison.Ordinal);
            if (manager != 0)
            {
                return manager;
            }

            var rank = left.Rank.CompareTo(right.Rank);
            if (rank != 0)
            {
                return rank;
            }

            var lastRank = left.LastRank.CompareTo(right.LastRank);
            if (lastRank != 0)
            {
                return lastRank;
            }

            var total = left.TotalPoints.CompareTo(right.TotalPoints);
            return total != 0
                ? total
                : left.MatchdayPoints.CompareTo(right.MatchdayPoints);
        });

        var builder = new StringBuilder();
        builder.Append("ucl-league:");
        builder.Append(standings.LeagueId.ToString(CultureInfo.InvariantCulture));
        builder.Append('\u001F');
        builder.Append(standings.PhaseId.ToString(CultureInfo.InvariantCulture));
        builder.Append('\u001F');
        builder.Append(standings.MatchdayId.ToString(CultureInfo.InvariantCulture));
        foreach (var row in rows)
        {
            builder.Append('\u001E');
            builder.Append(row.StableId.ToString(CultureInfo.InvariantCulture));
            builder.Append('\u001F');
            builder.Append(row.TeamName);
            builder.Append('\u001F');
            builder.Append(row.ManagerName);
            builder.Append('\u001F');
            builder.Append(row.Rank.ToString(CultureInfo.InvariantCulture));
            builder.Append('\u001F');
            builder.Append(row.LastRank.ToString(CultureInfo.InvariantCulture));
            builder.Append('\u001F');
            builder.Append(row.TotalPoints.ToString(CultureInfo.InvariantCulture));
            builder.Append('\u001F');
            builder.Append(row.MatchdayPoints.ToString(CultureInfo.InvariantCulture));
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record SnapshotRow(
        long StableId,
        string TeamName,
        string ManagerName,
        int Rank,
        int LastRank,
        int TotalPoints,
        int MatchdayPoints);
}
