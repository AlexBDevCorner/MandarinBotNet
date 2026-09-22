using System.Text;
using AngleSharp;
using AngleSharp.Dom;

namespace DiscordBot.EventWatch;

public sealed class RigaFcPageParser
{
    private static readonly string[] BlockSelectors =
    [
        "article",
        "section",
        "li",
        "div",
        "p",
        "h1",
        "h2",
        "h3",
        "h4",
        "h5",
        "h6",
        "td",
        "blockquote"
    ];

    public async Task<IReadOnlyList<EventWatchObservation>> ParseAsync(
        Uri sourceUrl,
        string html,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(
            request => request.Content(html).Address(sourceUrl),
            cancellationToken);

        return Parse(document, sourceUrl);
    }

    public IReadOnlyList<EventWatchObservation> Parse(
        string html,
        Uri sourceUrl)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(
            request => request.Content(html).Address(sourceUrl)).GetAwaiter().GetResult();

        return Parse(document, sourceUrl);
    }

    private static IReadOnlyList<EventWatchObservation> Parse(
        IDocument document,
        Uri sourceUrl)
    {
        var observations = new List<EventWatchObservation>();
        var source = sourceUrl.ToString();

        var selector = string.Join(", ", BlockSelectors);
        var blocks = document.QuerySelectorAll(selector);

        foreach (var block in blocks)
        {
            if (IsInsideExcludedChrome(block))
            {
                continue;
            }

            var text = NormalizeVisibleText(block.TextContent);
            if (text.Length == 0)
            {
                continue;
            }

            // Avoid mega-containers that merely wrap many smaller blocks:
            // keep the block only when it is reasonably scoped. Very large
            // containers (e.g. <body> or top-level layout divs) would merge
            // unrelated match mentions with site-wide ticket links and cause
            // false positives. Prefer leaf-ish semantic blocks.
            if (text.Length > 2000)
            {
                continue;
            }

            var anchors = ExtractAnchors(block, sourceUrl);
            var contextText = ExtractNearbyContext(block);

            observations.Add(new EventWatchObservation(
                source,
                text,
                anchors,
                contextText));
        }

        return observations;
    }

    private static List<EventWatchAnchor> ExtractAnchors(
        IElement block,
        Uri sourceUrl)
    {
        var anchors = new List<EventWatchAnchor>();
        var links = block.QuerySelectorAll("a[href]");

        foreach (var link in links)
        {
            if (IsInsideExcludedChrome(link))
            {
                continue;
            }

            var rawHref = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(rawHref))
            {
                continue;
            }

            var trimmed = rawHref.Trim();
            if (trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!Uri.TryCreate(sourceUrl, trimmed, out var absolute))
            {
                continue;
            }

            if (absolute.Scheme != Uri.UriSchemeHttp &&
                absolute.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            var anchorText = NormalizeVisibleText(link.TextContent);
            anchors.Add(new EventWatchAnchor(anchorText, absolute.ToString()));
        }

        return anchors;
    }

    private static string ExtractNearbyContext(IElement block)
    {
        // Nearby context is the heading inside the same semantic block.
        // Parent text is deliberately not included: a top-level parent such
        // as <body> would merge unrelated site-wide navigation (e.g. a
        // generic "Biletes" link) with a plain fixture mention and cause
        // false positives. Keeping context block-local preserves the
        // association between a match mention and its own ticket wording.
        var heading = block.QuerySelector("h1, h2, h3, h4, h5, h6");
        if (heading is null)
        {
            return string.Empty;
        }

        return NormalizeVisibleText(heading.TextContent);
    }

    private static bool IsInsideExcludedChrome(INode? node)
    {
        var current = node;
        while (current is not null)
        {
            if (current is IElement element &&
                IsExcludedChromeTag(element.TagName))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool IsExcludedChromeTag(string tagName)
    {
        return tagName.Equals("NAV", StringComparison.OrdinalIgnoreCase) ||
               tagName.Equals("HEADER", StringComparison.OrdinalIgnoreCase) ||
               tagName.Equals("FOOTER", StringComparison.OrdinalIgnoreCase) ||
               tagName.Equals("SCRIPT", StringComparison.OrdinalIgnoreCase) ||
               tagName.Equals("STYLE", StringComparison.OrdinalIgnoreCase) ||
               tagName.Equals("NOSCRIPT", StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeVisibleText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        var previousWasSpace = true;

        foreach (var character in raw)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasSpace = false;
        }

        return builder.ToString().Trim();
    }
}
