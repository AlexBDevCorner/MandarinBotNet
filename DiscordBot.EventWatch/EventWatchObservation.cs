namespace DiscordBot.EventWatch;

public sealed record EventWatchAnchor(
    string Text,
    string Url);

public sealed record EventWatchObservation(
    string SourceUrl,
    string Text,
    IReadOnlyList<EventWatchAnchor> Anchors,
    string Context);
