using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class EntryResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("started_event")]
    [JsonRequired]
    public int StartedEvent { get; init; }
}
