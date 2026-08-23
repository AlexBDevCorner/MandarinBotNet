using System.Globalization;
using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.BenchWarming;

public sealed class BenchWarmingMessageComposer
{
    private const int TopRoundStandingsCount = 3;
    private const string LeagueTitle = "🔥 Лига Обогревателей Скамейки";

    public string ComposeRoundSummary(BenchWarmingRoundResult round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var summary = new StringBuilder(
            $"{LeagueTitle} — итоги тура {round.EventId.ToString(CultureInfo.InvariantCulture)} " +
            $"(сезон {round.Season}):");

        if (round.RoundStandings.Count > 0)
        {
            summary.Append("\n\n🪑 Больше всех очков оставили на скамейке в этом туре:");
            AppendStandings(summary, round.RoundStandings.Take(TopRoundStandingsCount));

            var topBenchPlayer = round.BenchPoints
                .OrderByDescending(benchPoint => benchPoint.Points)
                .ThenBy(benchPoint => benchPoint.PlayerWebName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(benchPoint => benchPoint.Points > 0);
            if (topBenchPlayer is not null)
            {
                summary.Append(
                    $"\n\n🏅 Главный обогреватель скамейки тура: {DiscordTextSafety.SanitizeExternalName(topBenchPlayer.PlayerWebName)} — " +
                    $"{topBenchPlayer.Points.ToString(CultureInfo.InvariantCulture)} очков, греющих лавку команды " +
                    $"{DiscordTextSafety.SanitizeExternalName(topBenchPlayer.EntryName)}!");
            }
        }

        summary.Append($"\n\n📊 Общий зачёт сезона {round.Season}:");
        AppendStandings(summary, round.SeasonStandings);

        return summary.ToString();
    }

    public string ComposeSeasonStandings(string season, IEnumerable<BenchWarmingEntryStanding> standings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentNullException.ThrowIfNull(standings);

        var summary = new StringBuilder(
            $"{LeagueTitle} (сезон {season}):");
        var count = AppendStandings(summary, standings);
        if (count == 0)
        {
            summary.Append(
                "\n✨ Пока никто не греет скамейку — очки на лавке ещё не потеряны.");
        }

        return summary.ToString();
    }

    private static int AppendStandings(
        StringBuilder summary,
        IEnumerable<BenchWarmingEntryStanding> standings)
    {
        var count = 0;
        foreach (var standing in standings)
        {
            summary.Append('\n');
            summary.Append(
                $"{GetRankLabel(count + 1)} {DiscordTextSafety.SanitizeExternalName(standing.EntryName)} — " +
                $"{standing.Points.ToString(CultureInfo.InvariantCulture)}");
            count++;
        }

        return count;
    }

    private static string GetRankLabel(int rank) => rank switch
    {
        1 => ":one:",
        2 => ":two:",
        3 => ":three:",
        _ => $"{rank.ToString(CultureInfo.InvariantCulture)}."
    };
}
