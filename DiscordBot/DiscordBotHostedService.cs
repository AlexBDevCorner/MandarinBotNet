using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot
{
    public class DiscordBotHostedService(
        DiscordBotSettings settings,
        DiscordSocketClient client,
        IDiscordGatewayConnection gatewayConnection,
        DiscordConnectionReadiness readiness,
        ILogger<DiscordBotHostedService> logger) : IHostedService
    {
        private Dictionary<string, Func<SocketSlashCommand, Task>> _commandHandlers = []; 

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting Discord bot.");

            if (string.IsNullOrWhiteSpace(settings.Token))
            {
                throw new InvalidOperationException("BOT_TOKEN must be configured.");
            }

            _commandHandlers = new()
            {
                { "hugme", HandleHugMeCommand }
            };

            client.Log += LogAsync;
            gatewayConnection.Ready += ReadyAsync;
            gatewayConnection.Disconnected += DisconnectedAsync;
            client.GuildAvailable += GuildAvailableAsync;  // Triggered when a guild becomes available
            client.SlashCommandExecuted += SlashCommandHandler;

            try
            {
                await gatewayConnection.LoginAsync(settings.Token).WaitAsync(cancellationToken);
                await gatewayConnection.StartAsync().WaitAsync(cancellationToken);
                await readiness.WaitUntilReadyAsync(cancellationToken);
            }
            catch
            {
                readiness.MarkDisconnected();
                DetachEventHandlers();
                throw;
            }

            logger.LogInformation("Discord bot startup completed after gateway readiness.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            readiness.MarkDisconnected();

            try
            {
                await gatewayConnection.StopAsync().WaitAsync(cancellationToken);
            }
            finally
            {
                DetachEventHandlers();
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            readiness.MarkReady();
            logger.LogInformation("Discord gateway is ready.");
            return Task.CompletedTask;
        }

        private Task DisconnectedAsync(Exception exception)
        {
            readiness.MarkDisconnected();
            logger.LogWarning(exception, "Discord gateway disconnected; scheduled jobs will wait for readiness.");
            return Task.CompletedTask;
        }

        private void DetachEventHandlers()
        {
            client.Log -= LogAsync;
            gatewayConnection.Ready -= ReadyAsync;
            gatewayConnection.Disconnected -= DisconnectedAsync;
            client.GuildAvailable -= GuildAvailableAsync;
            client.SlashCommandExecuted -= SlashCommandHandler;
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

