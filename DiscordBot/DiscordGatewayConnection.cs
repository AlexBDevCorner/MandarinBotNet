using Discord;
using Discord.WebSocket;

namespace DiscordBot;

public interface IDiscordGatewayConnection
{
    event Func<LogMessage, Task> Log;

    event Func<Task> Ready;

    event Func<Exception, Task> Disconnected;

    event Func<SocketSlashCommand, Task> SlashCommandExecuted;

    Task LoginAsync(string token);

    Task StartAsync();

    Task LogoutAsync();
}

public sealed class DiscordGatewayConnection(DiscordSocketClient client) : IDiscordGatewayConnection
{
    public event Func<LogMessage, Task> Log
    {
        add => client.Log += value;
        remove => client.Log -= value;
    }

    public event Func<Task> Ready
    {
        add => client.Ready += value;
        remove => client.Ready -= value;
    }

    public event Func<Exception, Task> Disconnected
    {
        add => client.Disconnected += value;
        remove => client.Disconnected -= value;
    }

    public event Func<SocketSlashCommand, Task> SlashCommandExecuted
    {
        add => client.SlashCommandExecuted += value;
        remove => client.SlashCommandExecuted -= value;
    }

    public Task LoginAsync(string token)
    {
        return client.LoginAsync(TokenType.Bot, token);
    }

    public Task StartAsync()
    {
        return client.StartAsync();
    }

    public Task LogoutAsync()
    {
        return client.LogoutAsync();
    }
}
