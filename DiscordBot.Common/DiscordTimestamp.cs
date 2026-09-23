using System.Globalization;

namespace DiscordBot;

/// <summary>
/// Formats Discord-native timestamp markup from UTC instants.
/// </summary>
public static class DiscordTimestamp
{
    public static string FormatAbsolute(DateTimeOffset utcInstant)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<t:{utcInstant.ToUnixTimeSeconds()}:F>");
    }

    public static string FormatRelative(DateTimeOffset utcInstant)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<t:{utcInstant.ToUnixTimeSeconds()}:R>");
    }
}
