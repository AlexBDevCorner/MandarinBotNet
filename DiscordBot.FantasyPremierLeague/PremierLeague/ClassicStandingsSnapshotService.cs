using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DiscordBot.Responses;

namespace DiscordBot.PremierLeague;

/// <summary>
/// Computes a stable, deterministic fingerprint of the meaningful classic
/// league results. The fingerprint captures rank/order, total points, current
/// gameweek points, participants and winner/tie information that materially
/// affects the published Discord post, while excluding volatile timestamps,
/// formatting and randomly selected congratulations phrases.
/// An identical API snapshot produces the same fingerprint regardless of
/// response enumeration order or process restarts.
/// </summary>
public sealed class ClassicStandingsSnapshotService
{
    public string ComputeFingerprint(
        int leagueId,
        IEnumerable<ClassicStanding> standings)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);
        ArgumentNullException.ThrowIfNull(standings);

        var rows = standings
            .Select(result => new SnapshotRow(
                result.Entry,
                result.EntryName ?? string.Empty,
                result.Rank,
                result.LastRank,
                result.Total,
                result.EventTotal))
            .ToArray();

        // Stable ordering by the manager identifier so input enumeration
        // order never affects the comparison outcome.
        Array.Sort(rows, static (left, right) =>
        {
            var entry = left.Entry.CompareTo(right.Entry);
            if (entry != 0)
            {
                return entry;
            }

            var name = string.Compare(
                left.EntryName,
                right.EntryName,
                StringComparison.Ordinal);
            if (name != 0)
            {
                return name;
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

            var total = left.Total.CompareTo(right.Total);
            return total != 0
                ? total
                : left.EventTotal.CompareTo(right.EventTotal);
        });

        var builder = new StringBuilder();
        builder.Append("classic-league:");
        builder.Append(leagueId.ToString(CultureInfo.InvariantCulture));
        foreach (var row in rows)
        {
            // Unit/record separators avoid collisions when names contain
            // ordinary punctuation.
            builder.Append('\u001E');
            builder.Append(row.Entry.ToString(CultureInfo.InvariantCulture));
            builder.Append('\u001F');
            builder.Append(row.EntryName);
            builder.Append('\u001F');
            builder.Append(row.Rank.ToString(CultureInfo.InvariantCulture));
            builder.Append('\u001F');
            builder.Append(row.LastRank.ToString(CultureInfo.InvariantCulture));
            builder.Append('\u001F');
            builder.Append(row.Total.ToString(CultureInfo.InvariantCulture));
            builder.Append('\u001F');
            builder.Append(row.EventTotal.ToString(CultureInfo.InvariantCulture));
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record SnapshotRow(
        int Entry,
        string EntryName,
        int Rank,
        int LastRank,
        int Total,
        int EventTotal);
}
