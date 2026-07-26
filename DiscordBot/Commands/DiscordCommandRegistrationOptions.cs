using Microsoft.Extensions.Configuration;

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
    public const string ModeConfigurationKey = "Discord:Commands:RegistrationMode";
    public const string GuildIdConfigurationKey = "Discord:Commands:GuildId";

    public static DiscordCommandRegistrationOptions FromConfiguration(
        IConfiguration configuration)
    {
        var configuredMode = configuration[ModeConfigurationKey];
        var mode = string.IsNullOrWhiteSpace(configuredMode)
            ? DiscordCommandRegistrationMode.Global
            : Enum.TryParse<DiscordCommandRegistrationMode>(
                configuredMode,
                ignoreCase: true,
                out var parsedMode)
                ? parsedMode
                : throw new InvalidOperationException(
                    $"{ModeConfigurationKey} must be Disabled, Global, or Guild.");

        ulong? guildId = null;
        if (mode == DiscordCommandRegistrationMode.Guild)
        {
            var configuredGuildId = configuration[GuildIdConfigurationKey];
            if (!ulong.TryParse(configuredGuildId, out var parsedGuildId) ||
                parsedGuildId == 0)
            {
                throw new InvalidOperationException(
                    $"{GuildIdConfigurationKey} must contain a Discord guild ID " +
                    $"when {ModeConfigurationKey} is Guild.");
            }

            guildId = parsedGuildId;
        }

        return new DiscordCommandRegistrationOptions(mode, guildId);
    }

    public string TargetDescription => Mode switch
    {
        DiscordCommandRegistrationMode.Global => "global",
        DiscordCommandRegistrationMode.Guild => $"guild {GuildId}",
        _ => "disabled"
    };
}
