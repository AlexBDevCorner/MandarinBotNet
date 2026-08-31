namespace DiscordBot.FantasyPremierLeague.ChipWatch;

public static class FplChipWatchSourceIdentifier
{
    public static string Create(int eventId) => $"fpl-chip-watch-gw-{eventId}";
}
