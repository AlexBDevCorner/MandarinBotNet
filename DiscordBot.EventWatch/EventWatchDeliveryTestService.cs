using DiscordBot;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.EventWatch;

public sealed record EventWatchTestTargetResult(
    string WatchId,
    string WatchTitle,
    ulong GuildId,
    ulong ChannelId,
    bool Delivered,
    string? FailureReason);

public sealed record EventWatchTestDeliveryReport(
    bool HasConfiguredTargets,
    IReadOnlyList<EventWatchTestTargetResult> Targets);

public sealed class EventWatchDeliveryTestService(
    IOptions<MandarinBotOptions> options,
    IDiscordNotificationChannelResolver channelResolver,
    ILogger<EventWatchDeliveryTestService> logger)
{
    internal const int DiscordMessageLimit = 2_000;

    public async Task<EventWatchTestDeliveryReport> SendTestAsync(
        CancellationToken cancellationToken)
    {
        var watches = options.Value.EventWatch.Watches
            .Where(static watch => watch.Enabled && watch.Targets.Count > 0)
            .ToList();

        if (watches.Count == 0)
        {
            return new EventWatchTestDeliveryReport(
                HasConfiguredTargets: false,
                Targets: []);
        }

        var results = new List<EventWatchTestTargetResult>();
        foreach (var watch in watches)
        {
            foreach (var target in watch.Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await SendToTargetAsync(watch, target, cancellationToken));
            }
        }

        return new EventWatchTestDeliveryReport(
            HasConfiguredTargets: true,
            Targets: results);
    }

    public static string ComposeTestMessage(
        EventWatchDefinition watch,
        NotificationTargetOptions target)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(target);

        var safeTitle = DiscordTextSafety.SanitizeExternalName(watch.Title);
        return
            "🧪 **EventWatch delivery test**\n" +
            $"Watch: {safeTitle} (`{watch.Id.Trim()}`)\n" +
            $"Target: guild {target.GuildId}, channel {target.ChannelId}\n" +
            "This is a manual diagnostic message. " +
            "No notification checkpoints were consumed and no everyone mention was used.\n" +
            "If you see this in the configured EventWatch channel, Discord delivery works.";
    }

    private async Task<EventWatchTestTargetResult> SendToTargetAsync(
        EventWatchDefinition watch,
        NotificationTargetOptions target,
        CancellationToken cancellationToken)
    {
        var message = ComposeTestMessage(watch, target);

        var destination = channelResolver.Resolve(target.GuildId, target.ChannelId);
        if (!destination.GuildAvailable)
        {
            logger.LogWarning(
                "EventWatch delivery test skipped for watch {WatchId}: guild {GuildId} unavailable with outcome {Outcome}.",
                watch.Id,
                target.GuildId,
                "SkippedGuildUnavailable");
            return new EventWatchTestTargetResult(
                watch.Id,
                watch.Title,
                target.GuildId,
                target.ChannelId,
                Delivered: false,
                FailureReason: "guild unavailable");
        }

        if (destination.Channel is null)
        {
            logger.LogWarning(
                "EventWatch delivery test skipped for watch {WatchId}: channel {ChannelId} unavailable in guild {GuildId} with outcome {Outcome}.",
                watch.Id,
                target.ChannelId,
                target.GuildId,
                "SkippedChannelUnavailable");
            return new EventWatchTestTargetResult(
                watch.Id,
                watch.Title,
                target.GuildId,
                target.ChannelId,
                Delivered: false,
                FailureReason: "channel unavailable");
        }

        try
        {
            var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await destination.Channel.SendMessageAsync(
                    chunk,
                    allowEveryoneMention: false);
            }

            logger.LogInformation(
                "EventWatch delivery test delivered for watch {WatchId} to guild {GuildId}, channel {ChannelId} with outcome {Outcome}.",
                watch.Id,
                target.GuildId,
                target.ChannelId,
                "Delivered");
            return new EventWatchTestTargetResult(
                watch.Id,
                watch.Title,
                target.GuildId,
                target.ChannelId,
                Delivered: true,
                FailureReason: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "EventWatch delivery test failed for watch {WatchId} to guild {GuildId}, channel {ChannelId} with outcome {Outcome}.",
                watch.Id,
                target.GuildId,
                target.ChannelId,
                "Failed");
            return new EventWatchTestTargetResult(
                watch.Id,
                watch.Title,
                target.GuildId,
                target.ChannelId,
                Delivered: false,
                FailureReason: "delivery failed");
        }
    }
}
