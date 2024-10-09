using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DiscordBot
{
    public class DiscordBotHostedService(IConfiguration configuration, DiscordSocketClient client) : IHostedService
    {
        private Dictionary<string, Func<SocketSlashCommand, Task>> _commandHandlers = []; 

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _commandHandlers = new()
            {
                { "hugme", HandleHugMeCommand }
            };

            client.Log += LogAsync;
            client.Ready += ReadyAsync;
            client.GuildAvailable += GuildAvailableAsync;  // Triggered when a guild becomes available
            client.SlashCommandExecuted += SlashCommandHandler;

            var token = configuration["BOT_TOKEN"];

            // Log in and start bot
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await client.StopAsync();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            Console.WriteLine("Bot is connected and ready.");
        }

        // This method is called every time a guild becomes available to the bot (including when it joins new ones)
        private async Task GuildAvailableAsync(SocketGuild guild)
        {
            Console.WriteLine($"Bot is available in guild: {guild.Name} (ID: {guild.Id})");

            // Register commands for this guild
            var commands = new List<SlashCommandBuilder>
        {
            new SlashCommandBuilder().WithName("hugme").WithDescription("Hugs you!"),
        };

            await guild.DeleteApplicationCommandsAsync();

            foreach (var command in commands)
            {
                try
                {
                    await guild.CreateApplicationCommandAsync(command.Build());
                    Console.WriteLine($"Registered command: {command.Name} in guild {guild.Name}");
                }
                catch (HttpException e)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(e.Errors, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(json);
                }
            }
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            if (_commandHandlers.TryGetValue(command.Data.Name, out var handler))
            {
                await handler(command);
            }
            else
            {
                await command.RespondAsync("Unknown command");
            }
        }

        private async Task HandleHugMeCommand(SocketSlashCommand command)
        {
            var user = command.User;

            await command.RespondAsync($"{user.Mention} :people_hugging:");
        }
    }
}

