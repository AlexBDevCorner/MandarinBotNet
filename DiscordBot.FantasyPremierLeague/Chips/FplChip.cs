namespace DiscordBot.FantasyPremierLeague.Chips;

public sealed record FplChip(
    string Name,
    string Emoji,
    string Description,
    IReadOnlyList<string> BestUseCases,
    IReadOnlyList<string> Tips);
