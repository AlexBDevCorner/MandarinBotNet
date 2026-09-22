using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.EventWatch;

public sealed class EventWatchMessageCompositionService
{
    public string ComposeAnnouncement(
        string watchTitle,
        string sourceUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);

        var safeTitle = DiscordTextSafety.SanitizeExternalName(watchTitle);
        var builder = new StringBuilder();
        builder.Append("🎟️ **");
        builder.Append(safeTitle);
        builder.AppendLine(" — ticket sale announced**");
        builder.AppendLine();
        builder.Append("Riga FC's official site mentions ticket sales for this event.");
        builder.AppendLine();
        builder.Append("Source: ");
        builder.Append(sourceUrl.Trim());
        builder.AppendLine();
        builder.Append("Check the official Riga FC page for details before buying.");

        return builder.ToString().TrimEnd();
    }

    public string ComposeTicketLink(
        string watchTitle,
        string ticketUrl,
        string sourceUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);

        var safeTitle = DiscordTextSafety.SanitizeExternalName(watchTitle);
        var builder = new StringBuilder();
        builder.Append("🎫 **");
        builder.Append(safeTitle);
        builder.AppendLine(" — ticket link available**");
        builder.AppendLine();
        builder.Append("Ticket link: ");
        builder.Append(ticketUrl.Trim());
        builder.AppendLine();
        builder.Append("Detected on Riga FC's official site: ");
        builder.Append(sourceUrl.Trim());

        return builder.ToString().TrimEnd();
    }

    public string Compose(
        EventWatchSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        return signal.Kind switch
        {
            EventWatchSignalKind.Announcement => ComposeAnnouncement(
                signal.WatchTitle,
                signal.SourceUrl),
            EventWatchSignalKind.TicketLinkAvailable when signal.TicketUrl is not null =>
                ComposeTicketLink(
                    signal.WatchTitle,
                    signal.TicketUrl,
                    signal.SourceUrl),
            _ => ComposeAnnouncement(signal.WatchTitle, signal.SourceUrl)
        };
    }
}
