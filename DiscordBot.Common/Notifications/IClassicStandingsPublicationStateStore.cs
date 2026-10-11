namespace DiscordBot.Notifications
{
    /// <summary>
    /// The most recently successfully published classic standings snapshot for
    /// one Discord notification target. <see cref="Sequence"/> advances by one
    /// on every successful publication so each publication consumes a delivery
    /// checkpoint key that can never repeat.
    /// </summary>
    public sealed record ClassicStandingsPublicationState(
        string Fingerprint,
        long Sequence,
        DateTimeOffset UpdatedAtUtc);

    /// <summary>
    /// Persists the most recently successfully published classic standings
    /// snapshot per Discord notification target.
    /// Unlike <see cref="INotificationCheckpointStore"/>, which remembers every
    /// delivered event identifier, this store tracks only the latest published
    /// snapshot so a return to a previously seen table (A -&gt; B -&gt; A) is
    /// still recognized as a real change.
    /// </summary>
    public interface IClassicStandingsPublicationStateStore
    {
        ClassicStandingsPublicationState? GetPublicationState(
            ulong guildId,
            ulong channelId,
            int leagueId);

        void SetPublicationState(
            ulong guildId,
            ulong channelId,
            int leagueId,
            string fingerprint,
            long sequence,
            DateTimeOffset updatedAtUtc);
    }
}
