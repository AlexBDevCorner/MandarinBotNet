using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DiscordBot
{
    public class DiscordBotHostedService(IServiceProvider services, IConfiguration configuration) : IHostedService
    {
        private readonly DiscordSocketClient _client = new();
        private readonly CommandService _commands = new();
        private readonly IServiceProvider _services = services;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client.Log += Log;

            await RegisterCommandsAsync();

            
            var botToken = configuration["BOT_TOKEN"];

            await _client.LoginAsync(TokenType.Bot, botToken);
            await _client.StartAsync();
        }

        private async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;

            await _commands.AddModulesAsync(typeof(DiscordBotHostedService).Assembly, _services);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            var message = messageParam as SocketUserMessage;
            if (message == null || message.Author.IsBot) return;

            var context = new SocketCommandContext(_client, message);

            int argPos = 0;
            if (message.HasStringPrefix("!", ref argPos))
            {
                var result = await _commands.ExecuteAsync(context, argPos, _services);
                if (!result.IsSuccess)
                    Console.WriteLine(result.ErrorReason);
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.StopAsync();
        }
    }
}

