using System.Text;

namespace DiscordBot.Notifications;

public static class DiscordTextSafety
{
    public const int MaxExternalNameLength = 80;

    public static string SanitizeExternalName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "(без названия)";
        }

        var sanitized = new StringBuilder(MaxExternalNameLength);
        var previousWasSpace = false;

        foreach (var character in name.Trim())
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (!previousWasSpace && sanitized.Length < MaxExternalNameLength)
                {
                    sanitized.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            var requiredLength = character == '@' ? 2 : 1;
            if (sanitized.Length + requiredLength > MaxExternalNameLength)
            {
                break;
            }

            sanitized.Append(character);
            if (character == '@')
            {
                sanitized.Append('\u200B');
            }

            previousWasSpace = false;
        }

        return sanitized.ToString().TrimEnd();
    }
}
