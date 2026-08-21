using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class EntryEventPicksResponse
{
    [JsonPropertyName("picks")]
    public List<EntryEventPick> Picks { get; init; } = null!;

    [JsonPropertyName("entry_history")]
    public EntryEventHistory? EntryHistory { get; init; }

    [JsonPropertyName("automatic_subs")]
    public List<EntryAutomaticSubstitution> AutomaticSubstitutions { get; init; } = [];
}

public sealed class EntryEventPick
{
    [JsonPropertyName("element")]
    public int Element { get; init; }

    [JsonPropertyName("position")]
    public int Position { get; init; }

    [JsonPropertyName("multiplier")]
    public int Multiplier { get; init; }

    [JsonPropertyName("is_captain")]
    public bool IsCaptain { get; init; }

    [JsonPropertyName("is_vice_captain")]
    public bool IsViceCaptain { get; init; }
}

public sealed class EntryAutomaticSubstitution
{
    [JsonPropertyName("element_in")]
    public int ElementIn { get; init; }

    [JsonPropertyName("element_out")]
    public int ElementOut { get; init; }
}

public sealed class EntryEventHistory
{
    [JsonPropertyName("event_transfers")]
    public int EventTransfers { get; init; }

    [JsonPropertyName("event_transfers_cost")]
    public int EventTransfersCost { get; init; }
}
