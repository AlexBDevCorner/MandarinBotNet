namespace DiscordBot.Notifications
{
    /// <summary>
    /// Persists the most recently successfully published classic standings
    /// fingerprint per Discord notification target.
    /// Unlike <see cref="INotificationCheckpointStore"/>, which remembers every
    /// delivered event identifier, this store tracks only the latest published
    /// snapshot so a return to a previously seen table (A -&gt; B -&gt; A) is
    /// still recognized as a real change.
    /// </summary>
    public interface IClassicStandingsPublicationStateStore
    {
        string? GetLastPublishedFingerprint(
            ulong guildId,
            ulong channelId,
            int leagueId);

        void SetLastPublishedFingerprint(
            ulong guildId,
            ulong channelId,
            int leagueId,
            string fingerprint,
            DateTimeOffset updatedAtUtc);
    }
}
