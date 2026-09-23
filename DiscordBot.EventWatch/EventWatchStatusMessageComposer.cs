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

        builder.AppendLine("Ticket catalogue:");
        foreach (var page in report.SourcePages)
        {
            builder.Append("• ");
            builder.AppendLine(page.Trim());
        }

        builder.AppendLine("Calendar Tickets buttons are ignored: only catalogue products count.");

        if (!report.CollectionSucceeded)
        {
            builder.AppendLine("Catalogue: failed");
            builder.Append("Reason: ");
            builder.AppendLine(string.IsNullOrWhiteSpace(report.FailureReason)
                ? "unknown catalogue error"
                : report.FailureReason.Trim());
            builder.AppendLine("Result: ❌ Riga FC ticket catalogue fetch/parsing failed.");
            return builder.ToString().TrimEnd();
        }

        builder.Append("Catalogue: reachable, ");
        builder.Append(report.ObservationCount);
        builder.AppendLine(" products");

        foreach (var result in report.WatchResults)
        {
            builder.Append("• ");
            builder.Append(DiscordTextSafety.SanitizeExternalName(result.WatchTitle));
            builder.Append(" (`");
            builder.Append(result.WatchId.Trim());
            builder.Append("`): match [");
            builder.Append(string.Join(", ", result.MatchTerms));
            builder.Append("], ");
            builder.Append(result.TicketAvailableCount);
            builder.AppendLine(" ticket available");
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
            builder.AppendLine("Result: 🎟️ Opponent tickets detected in the catalogue (see evidence links).");
        }
        else
        {
            builder.AppendLine("Result: ✅ Healthy, catalogue reachable, no opponent tickets right now.");
        }

        return builder.ToString().TrimEnd();
    }
}
