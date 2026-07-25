using Discord;
using Discord.WebSocket;

namespace DiscordBot;

public interface IDiscordGatewayConnection
{
    event Func<Task> Ready;

    event Func<Exception, Task> Disconnected;

    Task LoginAsync(string token);

    Task StartAsync();

    Task StopAsync();
}

public sealed class DiscordGatewayConnection(DiscordSocketClient client) : IDiscordGatewayConnection
{
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

    public Task LoginAsync(string token)
    {
        return client.LoginAsync(TokenType.Bot, token);
    }

    public Task StartAsync()
    {
        return client.StartAsync();
    }

    public Task StopAsync()
    {
        return client.StopAsync();
    }
}
