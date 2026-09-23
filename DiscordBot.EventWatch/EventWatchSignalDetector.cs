using DiscordBot;

namespace DiscordBot.EventWatch;

public sealed class EventWatchSignalDetector
{
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

        // Each observation is one scoped ticket-catalogue product. A watch
        // produces at most one "ticket available" signal: the first product
        // whose own title, link text, or venue context names the opponent.
        // Generic packages, calendar buttons, news, or site-wide ticket links
        // are never observations, so they cannot trigger a signal.
        foreach (var observation in observations)
        {
            var searchable = CombineSearchableText(observation);
            if (!ContainsAny(searchable, matchTerms))
            {
                continue;
            }

            var productUrl = SelectProductUrl(observation);
            return
            [
                new EventWatchSignal(
                    EventWatchSignalKind.TicketAvailable,
                    watch.Id,
                    watch.Title,
                    productUrl,
                    TicketUrl: productUrl,
                    Evidence: TruncateEvidence(searchable))
            ];
        }

        return [];
    }

    private static string CombineSearchableText(EventWatchObservation observation)
    {
        var anchorText = string.Join(
            " ",
            observation.Anchors.Select(anchor => anchor.Text));
        var combined = $"{observation.Text} {anchorText} {observation.Context}";
        return combined.Trim();
    }

    private static string SelectProductUrl(EventWatchObservation observation)
    {
        foreach (var anchor in observation.Anchors)
        {
            if (Uri.TryCreate(anchor.Url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return anchor.Url;
            }
        }

        return observation.SourceUrl;
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
