using System.Text;

namespace DiscordBot.Notifications;

public static class DiscordMessageChunker
{
    public static IReadOnlyList<string> Split(string message, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var lines = message.ReplaceLineEndings("\n").Split('\n');
        var chunks = new List<string>();
        var current = new StringBuilder(maxLength);

        foreach (var line in lines)
        {
            if (line.Length > maxLength)
            {
                FlushCurrentChunk();

                var offset = 0;
                while (line.Length - offset > maxLength)
                {
                    chunks.Add(line.Substring(offset, maxLength));
                    offset += maxLength;
                }

                current.Append(line.AsSpan(offset));
                continue;
            }

            var separatorLength = current.Length == 0 ? 0 : 1;
            if (current.Length + separatorLength + line.Length > maxLength)
            {
                FlushCurrentChunk();
            }

            if (current.Length > 0)
            {
                current.Append('\n');
            }

            current.Append(line);
        }

        FlushCurrentChunk();
        return chunks;

        void FlushCurrentChunk()
        {
            if (current.Length == 0)
            {
                return;
            }

            chunks.Add(current.ToString());
            current.Clear();
        }
    }
}
