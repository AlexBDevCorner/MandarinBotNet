using DiscordBot;

namespace DiscordBot.EventWatch;

public sealed class EventWatchSignalDetector
{
    private static readonly string[] TicketSaleTerms =
    [
        "biļete",
        "biļetes",
        "biļešu",
        "ticket",
        "tickets",
        "pārdošanā",
        "iegādāties",
        "pirkt"
    ];

    private static readonly string[] TicketUrlCues =
    [
        "ticket",
        "bilet",
        "bile",
        "pirkt",
        "shop",
        "kase"
    ];

    public IReadOnlyList<EventWatchSignal> Detect(
        EventWatchDefinition watch,
        IReadOnlyList<EventWatchObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(observations);

        var matchTerms = watch.MatchTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .ToList();

        if (matchTerms.Count == 0 || observations.Count == 0)
        {
            return [];
        }

        EventWatchObservation? announcementEvidence = null;
        EventWatchObservation? ticketEvidence = null;
        string? ticketUrl = null;

        foreach (var observation in observations)
        {
            var searchable = CombineSearchableText(observation);
            if (!ContainsAny(searchable, matchTerms))
            {
                continue;
            }

            if (announcementEvidence is null &&
                ContainsAny(searchable, TicketSaleTerms))
            {
                announcementEvidence = observation;
            }

            if (ticketEvidence is null)
            {
                var actionable = FindActionableTicketLink(observation);
                if (actionable is not null)
                {
                    ticketEvidence = observation;
                    ticketUrl = actionable.Url;
                }
            }

            if (announcementEvidence is not null && ticketEvidence is not null)
            {
                break;
            }
        }

        var signals = new List<EventWatchSignal>(capacity: 2);

        if (announcementEvidence is not null)
        {
            signals.Add(new EventWatchSignal(
                EventWatchSignalKind.Announcement,
                watch.Id,
                watch.Title,
                announcementEvidence.SourceUrl,
                TicketUrl: null,
                Evidence: TruncateEvidence(CombineSearchableText(announcementEvidence))));
        }

        if (ticketEvidence is not null && ticketUrl is not null)
        {
            signals.Add(new EventWatchSignal(
                EventWatchSignalKind.TicketLinkAvailable,
                watch.Id,
                watch.Title,
                ticketEvidence.SourceUrl,
                TicketUrl: ticketUrl,
                Evidence: TruncateEvidence(CombineSearchableText(ticketEvidence))));
        }

        return signals;
    }

    private static string CombineSearchableText(EventWatchObservation observation)
    {
        if (observation.Anchors.Count == 0)
        {
            return string.IsNullOrEmpty(observation.Context)
                ? observation.Text
                : $"{observation.Text} {observation.Context}";
        }

        var anchorText = string.Join(
            " ",
            observation.Anchors.Select(anchor => anchor.Text));
        var combined = $"{observation.Text} {anchorText} {observation.Context}";
        return combined.Trim();
    }

    private static EventWatchAnchor? FindActionableTicketLink(
        EventWatchObservation observation)
    {
        foreach (var anchor in observation.Anchors)
        {
            if (string.IsNullOrWhiteSpace(anchor.Url))
            {
                continue;
            }

            if (!Uri.TryCreate(anchor.Url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            var anchorMentionsSale = ContainsAny(anchor.Text, TicketSaleTerms);
            var urlLooksLikeTicket = ContainsAny(anchor.Url, TicketUrlCues)
                || ContainsAny(uri.Host + uri.PathAndQuery, TicketUrlCues);

            if (anchorMentionsSale || urlLooksLikeTicket)
            {
                return anchor;
            }
        }

        return null;
    }

    private static bool ContainsAny(string text, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var term in terms)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }

            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string TruncateEvidence(string evidence)
    {
        if (evidence.Length <= 500)
        {
            return evidence;
        }

        return evidence[..500];
    }
}
