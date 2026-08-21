using DiscordBot.Commands;

namespace DiscordBot;

public sealed class MandarinBotOptions
{
    public const string SectionName = "Bot";

    public DiscordOptions Discord { get; init; } = new();

    public FantasyPremierLeagueOptions FantasyPremierLeague { get; init; } = new();

    public JobSchedulesOptions Schedules { get; init; } = new();

    public NotificationOptions Notifications { get; init; } = new();

    public WelcomeMessageOptions WelcomeMessages { get; init; } = new();
}

public sealed class DiscordOptions
{
    public string Token { get; init; } = string.Empty;

    public TimeSpan ReadinessTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public DiscordCommandOptions Commands { get; init; } = new();
}

public sealed class DiscordCommandOptions
{
    public DiscordCommandRegistrationMode RegistrationMode { get; init; } =
        DiscordCommandRegistrationMode.Disabled;

    public ulong? GuildId { get; init; }
}

public sealed class FantasyPremierLeagueOptions
{
    public const int DefaultMaxStandingsPages = 10;

    public int ClassicLeagueId { get; init; }

    public int HeadToHeadLeagueId { get; init; }

    public int MaxStandingsPages { get; init; } = DefaultMaxStandingsPages;
}

public sealed class JobSchedulesOptions
{
    public string TimeZoneId { get; init; } = string.Empty;

    public ScheduledJobOptions PremierLeagueNotifications { get; init; } = new();

    public ScheduledJobOptions UclFantasyNotifications { get; init; } = new();

    public ScheduledJobOptions ClassicStandings { get; init; } = new();

    public ScheduledJobOptions HeadToHeadStandings { get; init; } = new();

    public ScheduledJobOptions BenchWarmingLeague { get; init; } = new();

    public ScheduledJobOptions FplStatisticsCollection { get; init; } = new();

    public ScheduledJobOptions FplGameweekRecap { get; init; } = new();

    public bool HasEnabledJobs =>
        PremierLeagueNotifications.Enabled ||
        UclFantasyNotifications.Enabled ||
        ClassicStandings.Enabled ||
        HeadToHeadStandings.Enabled ||
        BenchWarmingLeague.Enabled ||
        FplStatisticsCollection.Enabled ||
        FplGameweekRecap.Enabled;

    public bool HasEnabledNotificationJobs =>
        PremierLeagueNotifications.Enabled ||
        UclFantasyNotifications.Enabled ||
        ClassicStandings.Enabled ||
        HeadToHeadStandings.Enabled ||
        BenchWarmingLeague.Enabled ||
        FplGameweekRecap.Enabled;
}

public sealed class ScheduledJobOptions
{
    public bool Enabled { get; init; }

    public string Cron { get; init; } = string.Empty;
}

public sealed class NotificationOptions
{
    public List<NotificationTargetOptions> Targets { get; init; } = [];
}

public sealed class NotificationTargetOptions
{
    public ulong GuildId { get; init; }

    public ulong ChannelId { get; init; }

    public bool MentionEveryone { get; init; }

    public string FormatMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return MentionEveryone ? $"@everyone {message}" : message;
    }
}

public sealed class WelcomeMessageOptions
{
    public bool Enabled { get; init; }

    public ulong GuildId { get; init; }

    public ulong ChannelId { get; init; }
}
