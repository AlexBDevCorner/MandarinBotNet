namespace DiscordBot.FantasyPremierLeague.Recognition;

/// <summary>
/// Persisted achievement keys that are no longer awarded but may still exist
/// in the recognition store as legacy rows. User-facing query and composition
/// code must ignore these keys so historical awards stay invisible and do not
/// affect current totals.
/// </summary>
public static class RetiredFplAchievementKeys
{
    public const string DifferentialMerchant = "differential-merchant";

    public static bool IsRetired(string achievementKey)
    {
        return string.Equals(
            achievementKey,
            DifferentialMerchant,
            StringComparison.Ordinal);
    }
}
