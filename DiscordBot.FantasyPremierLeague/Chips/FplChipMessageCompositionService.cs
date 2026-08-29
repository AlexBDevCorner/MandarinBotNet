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
            "В сезоне 2026/27 доступно по две фишки каждого типа: " +
            "Wildcard, Free Hit, Bench Boost и Triple Captain. " +
            "Первый набор предназначен для первой половины сезона " +
            "и сгорает после дедлайна GW19.");
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
