namespace DiscordBot.FantasyPremierLeague.Standings;

public enum FplStandingsLeague
{
    Both,
    Classic,
    HeadToHead
}

public sealed record FplStandingsQuery(
    FplStandingsLeague League,
    int? Top,
    string? Around);
