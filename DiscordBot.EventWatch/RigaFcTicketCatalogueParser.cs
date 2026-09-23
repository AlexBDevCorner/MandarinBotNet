using System.Text.Json;

namespace DiscordBot.EventWatch;

public sealed class RigaFcTicketCatalogueParser
{
    public IReadOnlyList<EventWatchObservation> Parse(
        string json,
        Uri sourceUrl)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new RigaFcApiException(
                $"The Riga FC ticket catalogue response from '{sourceUrl}' could not be parsed.",
                innerException: exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                throw new RigaFcApiException(
                    $"The Riga FC ticket catalogue response from '{sourceUrl}' has an unexpected shape.");
            }

            var observations = new List<EventWatchObservation>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = ReadString(item, "id");
                var name = ReadString(item, "name");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var sluggedName = ReadString(item, "sluggedName");
                var status = ReadString(item, "status");
                var venue = ReadVenueName(item);

                var productUrl = BuildProductUri(id.Trim(), sluggedName?.Trim());
                if (!seen.Add(productUrl))
                {
                    continue;
                }

                var contextParts = new List<string>(capacity: 2);
                if (!string.IsNullOrWhiteSpace(venue))
                {
                    contextParts.Add(venue.Trim());
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    contextParts.Add(status.Trim());
                }

                var trimmedName = name.Trim();
                var context = string.Join(" | ", contextParts);
                observations.Add(new EventWatchObservation(
                    productUrl,
                    trimmedName,
                    [new EventWatchAnchor(trimmedName, productUrl)],
                    context));
            }

            return observations;
        }
    }

    public static string BuildProductUri(string id, string? sluggedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var trimmedId = id.Trim();
        if (string.IsNullOrWhiteSpace(sluggedName))
        {
            return $"https://www.bilesuserviss.lv/biletes/{trimmedId}";
        }

        return $"https://www.bilesuserviss.lv/biletes/{trimmedId}/{sluggedName.Trim()}";
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static string? ReadVenueName(JsonElement item)
    {
        if (!item.TryGetProperty("venue", out var venue) ||
            venue.ValueKind != JsonValueKind.Object ||
            !venue.TryGetProperty("name", out var name) ||
            name.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return name.GetString();
    }
}
