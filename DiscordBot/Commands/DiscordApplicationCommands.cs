using Discord;

namespace DiscordBot.Commands;

public static class DiscordApplicationCommands
{
    public const string DeadlineName = "deadline";
    public const string HugMeName = "hugme";
    public const string StandingsName = "standings";
    public const string BenchLeagueName = "benchleague";
    public const string LiveInsightsName = "live";

    public static ApplicationCommandProperties[] BuildDesiredSet()
    {
        return
        [
            new SlashCommandBuilder()
                .WithName(HugMeName)
                .WithDescription("Обнимает вас! 🤗")
                .Build(),
            new SlashCommandBuilder()
                .WithName(StandingsName)
                .WithDescription("Показывает текущие таблицы лиг FPL. 🏆")
                .Build(),
            new SlashCommandBuilder()
                .WithName(DeadlineName)
                .WithDescription("Показывает ближайшие дедлайны FPL и ЛЧ по рижскому времени. ⏰")
                .Build(),
            new SlashCommandBuilder()
                .WithName(BenchLeagueName)
                .WithDescription("Показывает таблицу Лиги обогревателей скамейки. 🔥")
                .Build(),
            new SlashCommandBuilder()
                .WithName(LiveInsightsName)
                .WithDescription("Показывает результаты лиги FPL в реальном времени. ⚡")
                .Build()
        ];
    }
}
