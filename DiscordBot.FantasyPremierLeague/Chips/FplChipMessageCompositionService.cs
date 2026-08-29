using System.Text;

namespace DiscordBot.FantasyPremierLeague.Chips;

public sealed class FplChipMessageCompositionService
{
    public string Compose(IReadOnlyList<FplChip> chips)
    {
        var builder = new StringBuilder();

        builder.AppendLine("## 🃏 Фишки Fantasy Premier League");
        builder.AppendLine();
        builder.AppendLine(
            "В сезоне 2026/27 у тебя два набора фишек: " +
            "по одному Wildcard, Free Hit, Bench Boost и Triple Captain " +
            "на каждую половину сезона (GW1–19 и GW20+).");
        builder.AppendLine(
            "⚠️ Первый набор нужно использовать до дедлайна GW19 — " +
            "неиспользованные фишки не переносятся во вторую половину.");
        builder.AppendLine(
            "За один Gameweek можно активировать только одну фишку.");

        foreach (var chip in chips)
        {
            builder.AppendLine();
            builder.AppendLine($"### {chip.Emoji} {chip.Name}");
            builder.AppendLine(chip.Description);
            builder.AppendLine();
            builder.AppendLine("**Когда использовать:**");

            foreach (var useCase in chip.BestUseCases)
            {
                builder.AppendLine($"• {useCase}");
            }

            builder.AppendLine();
            builder.AppendLine("💡 " + string.Join(" ", chip.Tips));
        }

        builder.AppendLine();
        builder.AppendLine(
            "_Главное правило: не ищи идеальный момент бесконечно. " +
            "Хорошо использованная фишка лучше сгоревшей._");

        return builder.ToString();
    }
}
