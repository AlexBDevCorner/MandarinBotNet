using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordBot.WelcomeMessages;

public interface IWelcomeMessageHandler
{
    Task HandleAsync(DiscordGuildMember member);
}

public interface IWelcomeMessageDestinationResolver
{
    WelcomeMessageDestination Resolve(ulong guildId, ulong channelId);
}

public interface IWelcomeMessageChannel
{
    Task SendAsync(string imagePath, string content);
}

public sealed record WelcomeMessageDestination(
    bool GuildAvailable,
    IWelcomeMessageChannel? Channel);

public sealed record WelcomeMessageAsset(string Path);

public sealed class WelcomeMessageDestinationResolver(
    DiscordSocketClient discordClient) : IWelcomeMessageDestinationResolver
{
    public WelcomeMessageDestination Resolve(ulong guildId, ulong channelId)
    {
        var guild = discordClient.GetGuild(guildId);
        if (guild is null)
        {
            return new WelcomeMessageDestination(false, null);
        }

        var channel = guild.GetTextChannel(channelId);
        return new WelcomeMessageDestination(
            true,
            channel is null ? null : new DiscordWelcomeMessageChannel(channel));
    }

    private sealed class DiscordWelcomeMessageChannel(
        SocketTextChannel channel) : IWelcomeMessageChannel
    {
        public Task SendAsync(string imagePath, string content)
        {
            return channel.SendFileAsync(
                imagePath,
                content,
                allowedMentions: new AllowedMentions(AllowedMentionTypes.Users));
        }
    }
}

public sealed class WelcomeMessageTemplateRotator
{
    private static readonly string[] Templates =
    [
        "**{user} вошёл на сервер.** Мбаппе — диктатор, у Винисиуса новый подбородок, **Магуайр — свят. Мир стабилен.**",
        "Добро пожаловать, {user}. И главное — не паникуй. После семи с половиной миллионов лет вычислений Глубокомысленный наконец объявил ответ на главный вопрос жизни, Вселенной и всего такого.\n\n**Гарри Магуайр.**",
        "**Добро пожаловать, {user}.** Никогда не спрашивай женщину о возрасте, мужчину о зарплате и Гарри Магуайра, почему строительные нормы классифицируют его как несущую конструкцию.",
        "**Зафиксирован новый пользователь: {user}.** Спутники НАТО подтвердили: голова Магуайра по-прежнему видна из космоса.",
        "**СРОЧНО:** {user} присоединился к серверу. Совет Безопасности ООН собирается на экстренное заседание, чтобы понять, имеет ли Магуайр к этому отношение."
    ];

    private readonly Lock _lock = new();
    private int _nextIndex;

    public string Next(string userMention)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMention);

        string template;
        lock (_lock)
        {
            template = Templates[_nextIndex];
            _nextIndex = (_nextIndex + 1) % Templates.Length;
        }

        return template.Replace(
            "{user}",
            userMention,
            StringComparison.Ordinal);
    }
}

public sealed class WelcomeMessageHandler(
    WelcomeMessageOptions options,
    WelcomeMessageTemplateRotator templateRotator,
    IWelcomeMessageDestinationResolver destinationResolver,
    WelcomeMessageAsset asset,
    ILogger<WelcomeMessageHandler> logger) : IWelcomeMessageHandler
{
    public async Task HandleAsync(DiscordGuildMember member)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (options.GuildId == 0 || options.ChannelId == 0)
        {
            logger.LogWarning(
                "Welcome message configuration is incomplete for event {Event}; delivery finished with outcome {Outcome}.",
                "GuildMemberJoined",
                "SkippedInvalidConfiguration");
            return;
        }

        if (member.GuildId != options.GuildId)
        {
            return;
        }

        var destination = destinationResolver.Resolve(
            options.GuildId,
            options.ChannelId);
        if (!destination.GuildAvailable)
        {
            logger.LogWarning(
                "Configured welcome-message guild {GuildId} is unavailable for event {Event}; delivery finished with outcome {Outcome}.",
                options.GuildId,
                "GuildMemberJoined",
                "SkippedGuildUnavailable");
            return;
        }

        if (destination.Channel is null)
        {
            logger.LogWarning(
                "Configured welcome-message channel {ChannelId} was not found in guild {GuildId} for event {Event}; delivery finished with outcome {Outcome}.",
                options.ChannelId,
                options.GuildId,
                "GuildMemberJoined",
                "SkippedChannelUnavailable");
            return;
        }

        if (!File.Exists(asset.Path))
        {
            logger.LogError(
                "Welcome-message image {ImagePath} is unavailable for event {Event}; delivery finished with outcome {Outcome}.",
                asset.Path,
                "GuildMemberJoined",
                "SkippedImageUnavailable");
            return;
        }

        var message = templateRotator.Next(member.Mention);

        try
        {
            await destination.Channel.SendAsync(asset.Path, message);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Discord rejected the welcome message for guild {GuildId}, channel {ChannelId}, and user {UserId}; delivery finished with outcome {Outcome}.",
                options.GuildId,
                options.ChannelId,
                member.UserId,
                "DeliveryFailed");
        }
    }
}
