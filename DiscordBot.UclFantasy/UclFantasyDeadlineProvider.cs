using System.Globalization;
using DiscordBot.Deadlines;
using Microsoft.Extensions.Logging;
using TimeZoneConverter;

namespace DiscordBot.UclFantasy;

public sealed class UclFantasyDeadlineProvider(
    IUclFantasyClient uclFantasyClient,
    TimeProvider timeProvider,
    ILogger<UclFantasyDeadlineProvider> logger) : IUpcomingDeadlineProvider
{
    private static readonly TimeZoneInfo CentralEuropeanTimeZone =
        TZConvert.GetTimeZoneInfo("Europe/Paris");

    private static readonly string[] DeadlineFormats =
    [
        "MM/dd/yy hh:mm:ss tt",
        "MM/dd/yyyy HH:mm:ss"
    ];

    public string CompetitionName => "UCL";

    public async Task<CompetitionDeadline?> GetNextAsync(
        CancellationToken cancellationToken)
    {
        var webConfiguration = await uclFantasyClient.GetWebConfigurationAsync(
            cancellationToken);
        var configuration = webConfiguration.Data!.Value!;
        var fixtures = await uclFantasyClient.GetFixturesAsync(
            configuration,
            cancellationToken);

        var nowUtc = timeProvider.GetUtcNow();
        var candidates = fixtures.Data!.Value!
            .Where(matchday => matchday.IsLocked == 0)
            .Select(ParseDeadline)
            .Where(deadline => deadline is not null)
            .Select(deadline => deadline!.Value)
            .Where(deadline => deadline.DeadlineUtc > nowUtc)
            .OrderBy(deadline => deadline.DeadlineUtc)
            .ToArray();

        var currentDeadline = candidates.FirstOrDefault(
            deadline => deadline.IsCurrent);
        var selectedDeadline = currentDeadline.IsValid
            ? currentDeadline
            : candidates.FirstOrDefault();

        return selectedDeadline.IsValid
            ? new CompetitionDeadline(
                CompetitionName,
                "Matchday",
                selectedDeadline.MatchdayId,
                selectedDeadline.DeadlineUtc)
            : null;
    }

    private ParsedDeadline? ParseDeadline(UclFantasyMatchday matchday)
    {
        if (matchday.MatchdayId <= 0 ||
            string.IsNullOrWhiteSpace(matchday.Deadline))
        {
            return null;
        }

        if (!DateTime.TryParseExact(
                matchday.Deadline,
                DeadlineFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var localDeadline))
        {
            logger.LogWarning(
                "UCL Fantasy matchday {MatchdayId} has an unrecognized deadline value {Deadline}.",
                matchday.MatchdayId,
                matchday.Deadline);
            return null;
        }

        var unspecifiedLocalDeadline = DateTime.SpecifyKind(
            localDeadline,
            DateTimeKind.Unspecified);
        var deadlineUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(
                unspecifiedLocalDeadline,
                CentralEuropeanTimeZone));

        return new ParsedDeadline(
            matchday.MatchdayId,
            matchday.IsCurrent != 0,
            deadlineUtc);
    }

    private readonly record struct ParsedDeadline(
        int MatchdayId,
        bool IsCurrent,
        DateTimeOffset DeadlineUtc)
    {
        public bool IsValid => MatchdayId > 0;
    }
}
