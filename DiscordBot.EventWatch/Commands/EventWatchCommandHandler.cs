using System.Text;
using DiscordBot.Commands;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;

namespace DiscordBot.EventWatch.Commands;

public sealed class EventWatchCommandHandler(
    EventWatchStatusService statusService,
    EventWatchStatusMessageComposer statusComposer,
    EventWatchDeliveryTestService testService,
    ILogger<EventWatchCommandHandler> logger) : IEventWatchCommandHandler
{
    private const int DiscordMessageLimit = 2_000;

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var subcommand = interaction.GetSubcommandName()?.Trim().ToLowerInvariant();
        switch (subcommand)
        {
            case EventWatchCommandNames.StatusSubcommand:
                await HandleStatusAsync(interaction);
                break;
            case EventWatchCommandNames.TestSubcommand:
                await HandleTestAsync(interaction);
                break;
            default:
                await interaction.ModifyOriginalResponseAsync(
                    "ℹ️ Use `/eventwatch status` to check the Riga FC ticket catalogue or `/eventwatch test` to verify Discord delivery.");
                break;
        }
    }

    private async Task HandleStatusAsync(IDiscordSlashCommandInteraction interaction)
    {
        EventWatchStatusReport report;
        try
        {
            report = await statusService.GetStatusAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "EventWatch status command failed with outcome {Outcome}.",
                "Failed");
            await interaction.ModifyOriginalResponseAsync(
                "⚠️ EventWatch status is currently unavailable. Try again later.");
            return;
        }

        var message = statusComposer.Compose(report);
        await SendChunkedAsync(interaction, message);
    }

    private async Task HandleTestAsync(IDiscordSlashCommandInteraction interaction)
    {
        EventWatchTestDeliveryReport report;
        try
        {
            report = await testService.SendTestAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "EventWatch test command failed with outcome {Outcome}.",
                "Failed");
            await interaction.ModifyOriginalResponseAsync(
                "⚠️ EventWatch delivery test failed before reaching Discord. Try again later.");
            return;
        }

        await SendChunkedAsync(interaction, ComposeTestResult(report));
    }

    private static string ComposeTestResult(EventWatchTestDeliveryReport report)
    {
        if (!report.HasConfiguredTargets || report.Targets.Count == 0)
        {
            return "⚠️ EventWatch test found no enabled watches with Discord targets. " +
                "Configure an enabled EventWatch watch with a dedicated target first.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("🧪 **EventWatch delivery test**");
        foreach (var target in report.Targets)
        {
            var safeTitle = DiscordTextSafety.SanitizeExternalName(target.WatchTitle);
            if (target.Delivered)
            {
                builder.Append("✅ ");
                builder.Append(safeTitle);
                builder.Append(" (`");
                builder.Append(target.WatchId.Trim());
                builder.Append("`): test message sent to guild ");
                builder.Append(target.GuildId);
                builder.Append(", channel ");
                builder.Append(target.ChannelId);
                builder.AppendLine(".");
            }
            else
            {
                builder.Append("⚠️ ");
                builder.Append(safeTitle);
                builder.Append(" (`");
                builder.Append(target.WatchId.Trim());
                builder.Append("`): delivery to guild ");
                builder.Append(target.GuildId);
                builder.Append(", channel ");
                builder.Append(target.ChannelId);
                builder.Append(" failed (");
                builder.Append(target.FailureReason ?? "unknown reason");
                builder.AppendLine(").");
            }
        }

        builder.Append("No notification checkpoints were consumed and no everyone mention was used. ");
        builder.Append("The test is repeatable.");
        return builder.ToString().TrimEnd();
    }

    private static async Task SendChunkedAsync(
        IDiscordSlashCommandInteraction interaction,
        string message)
    {
        var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);
        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }
}
