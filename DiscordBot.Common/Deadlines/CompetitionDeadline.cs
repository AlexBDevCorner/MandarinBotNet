namespace DiscordBot.Deadlines;

public sealed record CompetitionDeadline(
    string CompetitionName,
    string RoundName,
    int RoundNumber,
    DateTimeOffset DeadlineUtc);
