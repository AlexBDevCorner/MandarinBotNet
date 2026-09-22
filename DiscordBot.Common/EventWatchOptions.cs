namespace DiscordBot;

public sealed class EventWatchOptions
{
    public List<EventWatchDefinition> Watches { get; init; } = [];
}

public sealed class EventWatchDefinition
{
    public bool Enabled { get; init; }

    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public List<string> MatchTerms { get; init; } = [];

    public List<NotificationTargetOptions> Targets { get; init; } = [];
}
