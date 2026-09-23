using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.EventWatch;

public sealed class EventWatchMessageCompositionService
{
    public string ComposeTicketAvailable(
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
        builder.AppendLine(" — tickets available**");
        builder.AppendLine();
        builder.Append("The Riga FC ticket catalogue lists a ticket for this event.");
        builder.AppendLine();
        builder.Append("Ticket: ");
        builder.Append(ticketUrl.Trim());
        builder.AppendLine();
        builder.Append("Catalogue: ");
        builder.Append(sourceUrl.Trim());

        return builder.ToString().TrimEnd();
    }

    public string Compose(
        EventWatchSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (signal.Kind == EventWatchSignalKind.TicketAvailable &&
            signal.TicketUrl is not null)
        {
            return ComposeTicketAvailable(
                signal.WatchTitle,
                signal.TicketUrl,
                signal.SourceUrl);
        }

        return ComposeTicketAvailable(
            signal.WatchTitle,
            signal.TicketUrl ?? signal.SourceUrl,
            signal.SourceUrl);
    }
}
