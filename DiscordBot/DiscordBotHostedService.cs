using Discord;
using Discord.WebSocket;
using DiscordBot.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot
{
    public class DiscordBotHostedService(
        DiscordBotSettings settings,
        DiscordSocketClient client,
        IDiscordGatewayConnection gatewayConnection,
        DiscordConnectionReadiness readiness,
        DiscordCommandRegistrationCoordinator commandRegistration,
        ILogger<DiscordBotHostedService> logger) : IHostedService
    {
        private const string DeploymentReadyMessage = "Bot is connected and ready.";
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
                { DiscordApplicationCommands.HugMeName, HandleHugMeCommand }
            };

            client.Log += LogAsync;
            gatewayConnection.Ready += ReadyAsync;
            gatewayConnection.Disconnected += DisconnectedAsync;
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

        private async Task ReadyAsync()
        {
            await commandRegistration.SynchronizeOnceAsync(CancellationToken.None);
            readiness.MarkReady();
            logger.LogInformation("{DeploymentReadyMessage}", DeploymentReadyMessage);
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
            client.SlashCommandExecuted -= SlashCommandHandler;
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

