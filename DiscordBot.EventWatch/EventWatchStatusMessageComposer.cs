using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.EventWatch;

public sealed class EventWatchStatusMessageComposer
{
    public string Compose(EventWatchStatusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.AppendLine("📡 **EventWatch status**");
        builder.Append("Scheduling: ");
        builder.AppendLine(report.SchedulingEnabled ? "enabled" : "disabled");

        if (report.EnabledWatches.Count == 0)
        {
            builder.AppendLine("Enabled watches: none");
        }
        else
        {
            builder.Append("Enabled watches (");
            builder.Append(report.EnabledWatches.Count);
            builder.AppendLine("):");
            foreach (var watch in report.EnabledWatches)
            {
                builder.Append("• ");
                builder.Append(DiscordTextSafety.SanitizeExternalName(watch.Title));
                builder.Append(" (`");
                builder.Append(watch.Id.Trim());
                builder.AppendLine("`)");
            }
        }

        builder.AppendLine("Sources:");
        foreach (var page in report.SourcePages)
        {
            builder.Append("• ");
            builder.AppendLine(page.Trim());
        }

        if (!report.CollectionSucceeded)
        {
            builder.AppendLine("Collection: failed");
            builder.Append("Reason: ");
            builder.AppendLine(string.IsNullOrWhiteSpace(report.FailureReason)
                ? "unknown collection error"
                : report.FailureReason.Trim());
            builder.AppendLine("Result: ❌ Riga FC collection/parsing failed.");
            return builder.ToString().TrimEnd();
        }

        builder.Append("Collection: succeeded, ");
        builder.Append(report.ObservationCount);
        builder.AppendLine(" observations");

        foreach (var result in report.WatchResults)
        {
            builder.Append("• ");
            builder.Append(DiscordTextSafety.SanitizeExternalName(result.WatchTitle));
            builder.Append(" (`");
            builder.Append(result.WatchId.Trim());
            builder.Append("`): match [");
            builder.Append(string.Join(", ", result.MatchTerms));
            builder.Append("], ");
            builder.Append(result.AnnouncementCount);
            builder.Append(" announcement, ");
            builder.Append(result.TicketLinkCount);
            builder.AppendLine(" ticket-link");
            foreach (var evidenceUrl in result.EvidenceSourceUrls)
            {
                builder.Append("  Evidence: ");
                builder.AppendLine(evidenceUrl.Trim());
            }
        }

        if (report.WatchResults.Count == 0)
        {
            builder.AppendLine("Result: ℹ️ No enabled watches configured.");
        }
        else if (report.HasSignals)
        {
            builder.AppendLine("Result: 🎟️ Qualifying ticket signals detected (see evidence links).");
        }
        else
        {
            builder.AppendLine("Result: ✅ Healthy, no qualifying signals right now.");
        }

        return builder.ToString().TrimEnd();
    }
}
