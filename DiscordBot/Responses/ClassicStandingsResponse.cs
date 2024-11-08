using System.Text.Json.Serialization;

namespace DiscordBot.Responses
{
    public class ClassicStandingsResponse
    {
        [JsonPropertyName("standings")]
        public ClassicStandings Standings { get; set; } = default!;
        [JsonPropertyName("last_updated_data")]
        public DateTime LastUpdatedData { get; set; }
    }

    public class ClassicStandings
    {
        [JsonPropertyName("results")]
        public List<ClassicStanding> Results { get; set; } = [];
    }

    public class ClassicStanding
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("event_total")]
        public int EventTotal { get; set; }
        [JsonPropertyName("player_name")]
        public string PlayerName { get; set; } = string.Empty;
        [JsonPropertyName("rank")]
        public int Rank { get; set; }
        [JsonPropertyName("last_rank")]
        public int LastRank { get; set; }
        [JsonPropertyName("total")]
        public int Total { get; set; }
        [JsonPropertyName("entry")]
        public int Entry { get; set; }
        [JsonPropertyName("entry_name")]
        public string EntryName { get; set; } = string.Empty;
    }
}
