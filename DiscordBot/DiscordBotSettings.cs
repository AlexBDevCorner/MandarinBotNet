namespace DiscordBot;

public sealed record DiscordBotSettings(
    string? Token,
    TimeSpan ReadinessTimeout);
