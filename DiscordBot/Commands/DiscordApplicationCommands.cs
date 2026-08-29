using Discord;

namespace DiscordBot.Commands;

public static class DiscordApplicationCommands
{
    public const string DeadlineName = "deadline";
    public const string HugMeName = "hugme";
    public const string StandingsName = "standings";
    public const string BenchLeagueName = "benchleague";
    public const string LiveInsightsName = "live";
    public const string ProfileName = "profile";
    public const string AchievementsName = "achievements";

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
                .AddOption(
                    new SlashCommandOptionBuilder()
                        .WithName("league")
                        .WithType(ApplicationCommandOptionType.String)
                        .WithDescription("Какую лигу показать")
                        .AddChoice("classic", "classic")
                        .AddChoice("h2h", "h2h"))
                .AddOption(
                    new SlashCommandOptionBuilder()
                        .WithName("top")
                        .WithType(ApplicationCommandOptionType.Integer)
                        .WithDescription("Показать только первые N команд")
                        .WithMinValue(1)
                        .WithMaxValue(20))
                .AddOption(
                    new SlashCommandOptionBuilder()
                        .WithName("around")
                        .WithType(ApplicationCommandOptionType.String)
                        .WithDescription(
                            "Показать команды рядом с указанным менеджером"))
                .Build(),
            new SlashCommandBuilder()
                .WithName(DeadlineName)
                .WithDescription("Показывает ближайшие дедлайны FPL и ЛЧ по рижскому времени. ⏰")
                .Build(),
            new SlashCommandBuilder()
                .WithName(BenchLeagueName)
                .WithDescription("Показывает таблицу Лиги обогревателей скамейки. 🔥")
                .AddOption(
                    new SlashCommandOptionBuilder()
                        .WithName("gw")
                        .WithType(ApplicationCommandOptionType.Integer)
                        .WithDescription("Показать результаты конкретного тура")
                        .WithMinValue(1)
                        .WithMaxValue(38))
                .AddOption(
                    new SlashCommandOptionBuilder()
                        .WithName("team")
                        .WithType(ApplicationCommandOptionType.String)
                        .WithDescription("Показать статистику конкретной команды"))
                .Build(),
            new SlashCommandBuilder()
                .WithName(LiveInsightsName)
                .WithDescription("Показывает результаты лиги FPL в реальном времени. ⚡")
                .Build(),
            new SlashCommandBuilder()
                .WithName(ProfileName)
                .WithDescription("Показывает профиль и достижения менеджера FPL. 🏅")
                .AddOption(
                    "manager",
                    ApplicationCommandOptionType.String,
                    "Название команды или имя менеджера",
                    isRequired: true)
                .Build(),
            new SlashCommandBuilder()
                .WithName(AchievementsName)
                .WithDescription("Показывает лидеров сезона по достижениям FPL. 🎖️")
                .Build()
        ];
    }
}
