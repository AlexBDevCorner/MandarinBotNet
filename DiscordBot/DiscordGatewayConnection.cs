using Discord;
using Discord.WebSocket;

namespace DiscordBot;

public interface IDiscordGatewayConnection
{
    event Func<LogMessage, Task> Log;

    event Func<Task> Ready;

    event Func<Exception, Task> Disconnected;

    event Func<SocketSlashCommand, Task> SlashCommandExecuted;

    event Func<DiscordGuildMember, Task> UserJoined;

    Task LoginAsync(string token);

    Task StartAsync();

    Task LogoutAsync();
}

public sealed record DiscordGuildMember(
    ulong GuildId,
    ulong UserId,
    string Mention);

public sealed class DiscordGatewayConnection : IDiscordGatewayConnection
{
    private readonly DiscordSocketClient _client;

    public DiscordGatewayConnection(DiscordSocketClient client)
    {
        _client = client;
        _client.UserJoined += HandleUserJoinedAsync;
    }

    public event Func<LogMessage, Task> Log
    {
        add => _client.Log += value;
        remove => _client.Log -= value;
    }

    public event Func<Task> Ready
    {
        add => _client.Ready += value;
        remove => _client.Ready -= value;
    }

    public event Func<Exception, Task> Disconnected
    {
        add => _client.Disconnected += value;
        remove => _client.Disconnected -= value;
    }

    public event Func<SocketSlashCommand, Task> SlashCommandExecuted
    {
        add => _client.SlashCommandExecuted += value;
        remove => _client.SlashCommandExecuted -= value;
    }

    public event Func<DiscordGuildMember, Task>? UserJoined;

    public Task LoginAsync(string token)
    {
        return _client.LoginAsync(TokenType.Bot, token);
    }

    public Task StartAsync()
    {
        return _client.StartAsync();
    }

    public Task LogoutAsync()
    {
        return _client.LogoutAsync();
    }

    private Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        return UserJoined?.Invoke(new DiscordGuildMember(
            user.Guild.Id,
            user.Id,
            user.Mention)) ?? Task.CompletedTask;
    }
}
