using System.Text.Json;
using Discord.Net;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Commands;

public interface IDiscordCommandSynchronizer
{
    Task SynchronizeAsync(CancellationToken cancellationToken);
}

public sealed class DiscordCommandSynchronizer(
    DiscordCommandRegistrationOptions options,
    IDiscordApplicationCommandClient commandClient,
    ILogger<DiscordCommandSynchronizer> logger) : IDiscordCommandSynchronizer
{
    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        if (options.Mode == DiscordCommandRegistrationMode.Disabled)
        {
            logger.LogInformation(
                "Discord application-command synchronization is disabled.");
            return;
        }

        var commands = DiscordApplicationCommands.BuildDesiredSet();
        logger.LogInformation(
            "Bulk overwriting {CommandCount} Discord application commands for {Target}. " +
            "Commands absent from the desired set will be removed intentionally.",
            commands.Length,
            options.TargetDescription);

        try
        {
            switch (options.Mode)
            {
                case DiscordCommandRegistrationMode.Global:
                    await commandClient.BulkOverwriteGlobalCommandsAsync(
                        commands,
                        cancellationToken);
                    break;
                case DiscordCommandRegistrationMode.Guild:
                    await commandClient.BulkOverwriteGuildCommandsAsync(
                        commands,
                        options.GuildId!.Value,
                        cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported command registration mode: {options.Mode}.");
            }
        }
        catch (HttpException exception)
        {
            logger.LogError(
                exception,
                "Discord rejected application-command synchronization for {Target}. " +
                "HTTP status: {HttpStatus}; Discord code: {DiscordCode}; " +
                "reason: {Reason}; validation errors: {ValidationErrors}.",
                options.TargetDescription,
                exception.HttpCode,
                exception.DiscordCode,
                exception.Reason,
                JsonSerializer.Serialize(exception.Errors));
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Application-command synchronization failed for {Target}. " +
                "Verify bot credentials, application-command permissions, and registration settings.",
                options.TargetDescription);
            throw;
        }

        logger.LogInformation(
            "Discord application commands synchronized successfully for {Target}.",
            options.TargetDescription);
    }
}
