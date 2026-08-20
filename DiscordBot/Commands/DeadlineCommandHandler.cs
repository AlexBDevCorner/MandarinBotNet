using System.Globalization;
using DiscordBot.Deadlines;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Commands;

public sealed class DeadlineCommandHandler(
    IEnumerable<IUpcomingDeadlineProvider> deadlineProviders,
    ConfiguredTimeZone configuredTimeZone,
    ILogger<DeadlineCommandHandler> logger) : IDeadlineCommandHandler
{
    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var providers = deadlineProviders.ToArray();
        var deadlines = new List<CompetitionDeadline>();
        var unavailableCompetitions = new List<string>();

        foreach (var provider in providers)
        {
            try
            {
                var deadline = await provider.GetNextAsync(CancellationToken.None);
                if (deadline is not null)
                {
                    deadlines.Add(deadline);
                }
            }
            catch (FantasyPremierLeagueApiException exception)
            {
                logger.LogWarning(
                    exception,
                    "FPL deadline command request failed with {FailureKind} and HTTP status {StatusCode}.",
                    exception.FailureKind,
                    exception.StatusCode);
                unavailableCompetitions.Add(provider.CompetitionName);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "{CompetitionName} deadline command request failed with {FailureKind}.",
                    provider.CompetitionName,
                    "Unexpected");
                unavailableCompetitions.Add(provider.CompetitionName);
            }
        }

        if (deadlines.Count == 0)
        {
            await interaction.ModifyOriginalResponseAsync(
                unavailableCompetitions.Count == 0
                    ? GetNoUpcomingDeadlineMessage(providers)
                    : string.Join(
                        "\n",
                        unavailableCompetitions.Select(GetUnavailableMessage)));
            return;
        }

        var messages = deadlines
            .OrderBy(deadline => deadline.DeadlineUtc)
            .Select(deadline =>
            {
                var localDeadline = configuredTimeZone.FromUtc(deadline.DeadlineUtc);
                return FormatDeadline(deadline, localDeadline);
            })
            .ToList();
        messages.AddRange(unavailableCompetitions.Select(GetUnavailableMessage));

        await interaction.ModifyOriginalResponseAsync(
            string.Join("\n", messages));
    }

    private static string GetUnavailableMessage(string competitionName)
    {
        return $"The {competitionName} deadline is unavailable right now. Please try again later.";
    }

    private static string GetNoUpcomingDeadlineMessage(
        IReadOnlyCollection<IUpcomingDeadlineProvider> providers)
    {
        return providers.Count == 1
            ? $"The next {providers.Single().CompetitionName} deadline is not available yet."
            : "No upcoming deadlines are available yet.";
    }

    private static string FormatDeadline(
        CompetitionDeadline deadline,
        DateTimeOffset localDeadline)
    {
        var offsetSign = localDeadline.Offset < TimeSpan.Zero ? "-" : "+";
        var absoluteOffset = localDeadline.Offset.Duration();
        var offset = string.Create(
            CultureInfo.InvariantCulture,
            $"UTC{offsetSign}{absoluteOffset:hh\\:mm}");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{deadline.CompetitionName} {deadline.RoundName} {deadline.RoundNumber} deadline: " +
            $"{localDeadline:dddd, d MMMM yyyy 'at' HH:mm} " +
            $"(Riga, Latvia, {offset}).");
    }
}
