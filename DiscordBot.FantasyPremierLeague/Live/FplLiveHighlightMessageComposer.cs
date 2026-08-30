using System.Globalization;
using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.Live;

public sealed class FplLiveHighlightMessageComposer
{
    public const int MaximumHighlightLines = 5;

    public string Compose(FplLiveDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        if (digest.Highlights.Count == 0)
        {
            throw new ArgumentException(
                "An FPL live digest must contain at least one highlight.",
                nameof(digest));
        }

        var message = new StringBuilder(
            $"⚡ FPL live — тур {digest.EventId.ToString(CultureInfo.InvariantCulture)}");
        foreach (var highlight in digest.Highlights.Take(MaximumHighlightLines))
        {
            message.Append('\n');
            AppendHighlight(message, highlight);
        }

        var remaining = digest.Highlights.Count - MaximumHighlightLines;
        if (remaining > 0)
        {
            message.Append("\n...и ещё ");
            AppendCount(message, remaining, "изменение", "изменения", "изменений");
            message.Append('.');
        }

        message.Append("\nПолная картина: /live");
        return message.ToString();
    }

    private static void AppendHighlight(
        StringBuilder message,
        FplLiveHighlight highlight)
    {
        switch (highlight)
        {
            case LeaderChangedHighlight leader:
                message.Append("🥇 ");
                message.Append(Sanitize(leader.NewLeaderName));
                message.Append(" выходит на первое место — ");
                AppendCount(message, leader.NewLeaderPoints, "очко", "очка", "очков");
                if (leader.Gap > 0)
                {
                    message.Append(", +");
                    message.Append(leader.Gap.ToString(CultureInfo.InvariantCulture));
                    message.Append(" от второго места");
                }

                message.Append('.');
                break;
            case SignificantRankChangeHighlight rank:
                message.Append("🔥 ");
                message.Append(Sanitize(rank.EntryName));
                message.Append(rank.CurrentRank < rank.PreviousRank
                    ? " поднялся с "
                    : " опустился с ");
                message.Append(FormatPreviousRank(rank.PreviousRank));
                message.Append(" на ");
                message.Append(FormatCurrentRank(rank.CurrentRank));
                message.Append(" место.");
                break;
            case BenchThresholdReachedHighlight bench:
                message.Append("🪑 ");
                message.Append(Sanitize(bench.EntryName));
                message.Append(" оставил ");
                AppendCount(message, bench.BenchPoints, "очко", "очка", "очков");
                message.Append(" на скамейке.");
                break;
            case CaptainSuccessHighlight captain:
                message.Append("🧠 ");
                message.Append(Sanitize(captain.CaptainName));
                message.Append(" принёс ");
                message.Append(Sanitize(captain.EntryName));
                message.Append(' ');
                AppendCount(
                    message,
                    captain.EffectivePoints,
                    "капитанское очко",
                    "капитанских очка",
                    "капитанских очков");
                message.Append('.');
                break;
            case CaptainDisasterHighlight disaster:
                message.Append("💥 У ");
                message.Append(Sanitize(disaster.EntryName));
                message.Append(" капитан ");
                message.Append(Sanitize(disaster.CaptainName));
                message.Append(" набрал ");
                message.Append(disaster.CaptainPoints.ToString(CultureInfo.InvariantCulture));
                message.Append(", а вице-капитан ");
                message.Append(Sanitize(disaster.ViceCaptainName));
                message.Append(" — ");
                message.Append(disaster.ViceCaptainPoints.ToString(CultureInfo.InvariantCulture));
                message.Append(".");
                break;
            case AutomaticSubstitutionHighlight substitution:
                message.Append("🛟 ");
                message.Append(Sanitize(substitution.EntryName));
                message.Append(" спас ");
                AppendCount(message, substitution.SavedPoints, "очко", "очка", "очков");
                message.Append(" автозаменой: ");
                message.Append(Sanitize(substitution.PlayerInName));
                message.Append(" вместо ");
                message.Append(Sanitize(substitution.PlayerOutName));
                message.Append('.');
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(highlight));
        }
    }

    private static string FormatPreviousRank(int rank)
    {
        return rank.ToString(CultureInfo.InvariantCulture) + "-го";
    }

    private static string FormatCurrentRank(int rank)
    {
        return rank.ToString(CultureInfo.InvariantCulture) + "-е";
    }

    private static string Sanitize(string value)
    {
        return DiscordTextSafety.SanitizeExternalName(value);
    }

    private static void AppendCount(
        StringBuilder message,
        int value,
        string singular,
        string few,
        string many)
    {
        message.Append(value.ToString(CultureInfo.InvariantCulture));
        message.Append(' ');

        var absoluteValue = Math.Abs((long)value);
        var lastTwoDigits = absoluteValue % 100;
        if (lastTwoDigits is >= 11 and <= 14)
        {
            message.Append(many);
            return;
        }

        message.Append((absoluteValue % 10) switch
        {
            1 => singular,
            2 or 3 or 4 => few,
            _ => many
        });
    }
}
