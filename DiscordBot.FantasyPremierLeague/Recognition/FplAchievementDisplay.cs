using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.Recognition;

public static class FplAchievementDisplay
{
    public static string GetName(
        string achievementKey,
        string fallbackName)
    {
        return achievementKey switch
        {
            FplAchievementKeys.FirstBlood => "🩸 Первая кровь",
            FplAchievementKeys.BenchWarmer => "🔥 Обогреватель скамейки",
            FplAchievementKeys.CaptainDisaster => "💥 Капитанская катастрофа",
            FplAchievementKeys.DifferentialMerchant => "💎 Повелитель дифференциалов",
            FplAchievementKeys.MinusEightEnjoyer => "💸 Любитель минус восьми",
            _ => DiscordTextSafety.SanitizeExternalName(fallbackName)
        };
    }
}
