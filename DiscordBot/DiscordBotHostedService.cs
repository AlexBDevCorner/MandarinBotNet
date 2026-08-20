using Discord;
using Discord.WebSocket;
using DiscordBot.Commands;
using DiscordBot.WelcomeMessages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot
{
    public class DiscordBotHostedService(
        DiscordOptions options,
        IDiscordGatewayConnection gatewayConnection,
        DiscordConnectionReadiness readiness,
        DiscordCommandRegistrationCoordinator commandRegistration,
        IDeadlineCommandHandler deadlineCommandHandler,
        IStandingsCommandHandler standingsCommandHandler,
        IBenchLeagueCommandHandler benchLeagueCommandHandler,
        IWelcomeMessageHandler welcomeMessageHandler,
        DiscordNetLogHandler discordLogHandler,
        ILogger<DiscordBotHostedService> logger) : IHostedService
    {
        private const string DeploymentReadyMessage = "Bot is connected and ready.";
        private Dictionary<string, Func<IDiscordSlashCommandInteraction, Task>>
            _commandHandlers = [];
        private int _readyCount;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting Discord bot.");

            if (string.IsNullOrWhiteSpace(options.Token))
            {
                throw new InvalidOperationException(
                    "Bot:Discord:Token must be configured by a secret provider.");
            }

            _commandHandlers = new()
            {
                { DiscordApplicationCommands.DeadlineName, deadlineCommandHandler.HandleAsync },
                { DiscordApplicationCommands.HugMeName, HandleHugMeCommand },
                { DiscordApplicationCommands.StandingsName, standingsCommandHandler.HandleAsync },
                { DiscordApplicationCommands.BenchLeagueName, benchLeagueCommandHandler.HandleAsync }
            };

            gatewayConnection.Log += discordLogHandler.HandleAsync;
            gatewayConnection.Ready += ReadyAsync;
            gatewayConnection.Disconnected += DisconnectedAsync;
            gatewayConnection.SlashCommandExecuted += SlashCommandHandler;
            gatewayConnection.UserJoined += welcomeMessageHandler.HandleAsync;

            try
            {
                await gatewayConnection.LoginAsync(options.Token).WaitAsync(cancellationToken);
                await gatewayConnection.StartAsync().WaitAsync(cancellationToken);
                await readiness.WaitUntilReadyAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                readiness.MarkDisconnected();
                DetachEventHandlers();
                logger.LogInformation(
                    "Discord bot startup ended with outcome {Outcome}.",
                    "Canceled");
                throw;
            }
            catch (Exception exception)
            {
                readiness.MarkDisconnected();
                DetachEventHandlers();
                logger.LogCritical(
                    exception,
                    "Discord bot startup ended with outcome {Outcome}.",
                    "Failed");
                throw;
            }

            logger.LogInformation(
                "Discord bot startup completed after gateway readiness with outcome {Outcome}.",
                "Ready");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            readiness.MarkDisconnected();
            logger.LogInformation("Stopping Discord bot.");
            DetachEventHandlers();

            try
            {
                await gatewayConnection.LogoutAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Discord bot shutdown ended with outcome {Outcome}.",
                    "Canceled");
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Discord bot shutdown ended with outcome {Outcome}.",
                    "Failed");
                throw;
            }
            logger.LogInformation(
                "Discord bot shutdown completed with outcome {Outcome}.",
                "Stopped");
        }

        private async Task ReadyAsync()
        {
            await commandRegistration.SynchronizeOnceAsync(CancellationToken.None);
            readiness.MarkReady();
            var gatewayEvent = Interlocked.Increment(ref _readyCount) == 1
                ? "Ready"
                : "Reconnected";
            logger.LogInformation(
                "{DeploymentReadyMessage} Gateway event {Event} completed with outcome {Outcome}.",
                DeploymentReadyMessage,
                gatewayEvent,
                "Ready");
        }

        private Task DisconnectedAsync(Exception exception)
        {
            readiness.MarkDisconnected();
            logger.LogWarning(
                exception,
                "Discord gateway event {Event} completed with outcome {Outcome}; scheduled jobs will wait for readiness.",
                "Disconnected",
                "WaitingForReadiness");
            return Task.CompletedTask;
        }

        private void DetachEventHandlers()
        {
            gatewayConnection.Log -= discordLogHandler.HandleAsync;
            gatewayConnection.Ready -= ReadyAsync;
            gatewayConnection.Disconnected -= DisconnectedAsync;
            gatewayConnection.SlashCommandExecuted -= SlashCommandHandler;
            gatewayConnection.UserJoined -= welcomeMessageHandler.HandleAsync;
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            await HandleSlashCommandAsync(
                new DiscordNetSlashCommandInteraction(command));
        }

        internal async Task HandleSlashCommandAsync(
            IDiscordSlashCommandInteraction command)
        {
            if (_commandHandlers.TryGetValue(command.Name, out var handler))
            {
                await handler(command);
            }
            else
            {
                await command.RespondAsync("Unknown command");
            }
        }

        private async Task HandleHugMeCommand(
            IDiscordSlashCommandInteraction command)
        {
            await command.RespondAsync(
                $"{command.UserMention} :people_hugging:");
        }
    }
}

