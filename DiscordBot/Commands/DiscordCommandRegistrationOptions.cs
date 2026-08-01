namespace DiscordBot.Commands;

public enum DiscordCommandRegistrationMode
{
    Disabled,
    Global,
    Guild
}

public sealed record DiscordCommandRegistrationOptions(
    DiscordCommandRegistrationMode Mode,
    ulong? GuildId)
{
    public string TargetDescription => Mode switch
    {
        DiscordCommandRegistrationMode.Global => "global",
        DiscordCommandRegistrationMode.Guild => $"guild {GuildId}",
        _ => "disabled"
    };
}
