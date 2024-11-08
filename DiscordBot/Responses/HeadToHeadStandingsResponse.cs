using System.Text.Json.Serialization;

namespace DiscordBot.Responses
{
    public class HeadToHeadStandingsResponse
    {
        [JsonPropertyName("standings")]
        public HeadToHeadStandings HeadToHeadStandings { get; set; } = default!;
    }

    public class HeadToHeadStandings
    {
        [JsonPropertyName("results")]
        public List<HeadToHeadStanding> Results { get; set; } = [];
    }

    public class HeadToHeadStanding 
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }
        [JsonPropertyName("last_rank")]
        public int LastRank { get; set; }
        [JsonPropertyName("total")]
        public int Total { get; set; }
        [JsonPropertyName("entry_name")]
        public string EntryName { get; set; } = string.Empty;
    }
}
