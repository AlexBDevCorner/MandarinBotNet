namespace DiscordBot.Deadlines;

public interface IUpcomingDeadlineProvider
{
    string CompetitionName { get; }

    Task<CompetitionDeadline?> GetNextAsync(
        CancellationToken cancellationToken);
}
