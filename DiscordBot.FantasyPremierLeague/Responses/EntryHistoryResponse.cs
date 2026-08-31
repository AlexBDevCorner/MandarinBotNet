using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class EntryHistoryResponse
{
    [JsonPropertyName("current")]
    public List<EntryHistoryGameweek> Current { get; init; } = [];

    [JsonPropertyName("chips")]
    public List<EntryHistoryChip> Chips { get; init; } = [];
}

public sealed class EntryHistoryGameweek
{
    [JsonPropertyName("event")]
    public int EventId { get; init; }

    [JsonPropertyName("points")]
    public int Points { get; init; }

    [JsonPropertyName("total_points")]
    public int TotalPoints { get; init; }

    [JsonPropertyName("event_transfers")]
    public int EventTransfers { get; init; }

    [JsonPropertyName("event_transfers_cost")]
    public int EventTransfersCost { get; init; }
}

public sealed class EntryHistoryChip
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("event")]
    public int EventId { get; init; }

    [JsonPropertyName("time")]
    public DateTimeOffset? PlayedAtUtc { get; init; }
}
