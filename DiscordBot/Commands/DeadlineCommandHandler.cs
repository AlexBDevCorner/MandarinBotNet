using System.Globalization;
using DiscordBot.Deadlines;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Commands;

public sealed class DeadlineCommandHandler(
    IUpcomingDeadlineProvider deadlineProvider,
    ConfiguredTimeZone configuredTimeZone,
    ILogger<DeadlineCommandHandler> logger) : IDeadlineCommandHandler
{
    private const string UnavailableMessage =
        "The FPL deadline is unavailable right now. Please try again later.";
    private const string NoUpcomingDeadlineMessage =
        "The next FPL deadline is not available yet.";

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        CompetitionDeadline? deadline;
        try
        {
            deadline = await deadlineProvider.GetNextAsync(CancellationToken.None);
        }
        catch (FantasyPremierLeagueApiException exception)
        {
            logger.LogWarning(
                exception,
                "FPL deadline command request failed with {FailureKind} and HTTP status {StatusCode}.",
                exception.FailureKind,
                exception.StatusCode);
            await interaction.ModifyOriginalResponseAsync(UnavailableMessage);
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "FPL deadline command request failed with {FailureKind}.",
                "Unexpected");
            await interaction.ModifyOriginalResponseAsync(UnavailableMessage);
            return;
        }

        if (deadline is null)
        {
            await interaction.ModifyOriginalResponseAsync(
                NoUpcomingDeadlineMessage);
            return;
        }

        var localDeadline = configuredTimeZone.FromUtc(deadline.DeadlineUtc);
        await interaction.ModifyOriginalResponseAsync(
            FormatDeadline(deadline, localDeadline));
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
