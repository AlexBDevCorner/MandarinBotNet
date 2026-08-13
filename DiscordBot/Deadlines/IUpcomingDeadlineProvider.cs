namespace DiscordBot.Deadlines;

public interface IUpcomingDeadlineProvider
{
    Task<CompetitionDeadline?> GetNextAsync(
        CancellationToken cancellationToken);
}
