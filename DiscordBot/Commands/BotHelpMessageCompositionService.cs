namespace DiscordBot.Commands;

public sealed class BotHelpMessageCompositionService
{
    public const string FplClassicLeagueUrl =
        "https://fantasy.premierleague.com/leagues/auto-join/n9ki9b";
    public const string FplHeadToHeadLeagueUrl =
        "https://fantasy.premierleague.com/leagues/auto-join/a5gnav";
    public const string UclLeagueUrl =
        "https://gaming.uefa.com/en/uclfantasy/leagues/3gp3eN/004D0061006700750069007200650020004D0065007300730069006100680020004C00650061006700750065/Bebrakungs";

    public string ComposeGuide()
    {
        return
            "🤖 **Что умеет MandarinBot**\n" +
            "`/live` — живая таблица FPL во время матчей\n" +
            "`/standings` — классическая и H2H таблицы, топ и соседи\n" +
            "`/profile` — профиль менеджера, трофеи и рейтинги\n" +
            "`/achievements` — лидеры сезона по достижениям\n" +
            "`/benchleague` — Лига обогревателей скамейки\n" +
            "`/prices` — последние изменения цен в составах нашей лиги\n" +
            "`/deadline` — ближайшие дедлайны FPL и ЛЧ\n" +
            "`/chips` — справочник по фишкам FPL\n" +
            "`/chipwatch` — доступные фишки и умные подсказки по их использованию\n" +
            "`/hugme` — священная функция обнимашек\n\n" +
            ComposeLeagueLinks();
    }

    public string ComposeWelcomeOnboarding()
    {
        return
            "**С чего начать**\n" +
            "1. Вступи в фэнтези-лиги по ссылкам ниже.\n" +
            "2. Проверь ближайший дедлайн командой `/deadline`.\n" +
            "3. Во время матчей открывай `/live`, после тура — `/profile` и `/benchleague`.\n" +
            "4. Полный список возможностей покажет `/help`.\n\n" +
            ComposeLeagueLinks();
    }

    private static string ComposeLeagueLinks()
    {
        return
            "**Наши лиги**\n" +
            $"[FPL Лига обнимашек]({FplClassicLeagueUrl})\n" +
            $"[FPL Лига обнимашек-к-обнимашкам]({FplHeadToHeadLeagueUrl})\n" +
            $"[Лига ЛЧ имени великого Мессии]({UclLeagueUrl})";
    }
}
