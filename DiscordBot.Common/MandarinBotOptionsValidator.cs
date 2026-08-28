using DiscordBot.Commands;
using Microsoft.Extensions.Options;
using Quartz;
using TimeZoneConverter;

namespace DiscordBot;

public sealed class MandarinBotOptionsValidator(bool requireOperationalConfiguration)
    : IValidateOptions<MandarinBotOptions>
{
    public ValidateOptionsResult Validate(string? name, MandarinBotOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Discord.Token))
        {
            failures.Add(
                "Bot:Discord:Token is required. Supply it through Bot__Discord__Token in the environment or another secret provider.");
        }

        if (options.Discord.ReadinessTimeout <= TimeSpan.Zero)
        {
            failures.Add("Bot:Discord:ReadinessTimeout must be greater than zero.");
        }

        if (options.Discord.Commands.RegistrationMode == DiscordCommandRegistrationMode.Guild &&
            options.Discord.Commands.GuildId is not > 0)
        {
            failures.Add(
                "Bot:Discord:Commands:GuildId must contain a Discord guild ID when RegistrationMode is Guild.");
        }

        ValidateTimeZone(options.Schedules.TimeZoneId, failures);
        ValidateSchedule(
            "Bot:Schedules:PremierLeagueNotifications",
            options.Schedules.PremierLeagueNotifications,
            failures);
        ValidateSchedule(
            "Bot:Schedules:UclFantasyNotifications",
            options.Schedules.UclFantasyNotifications,
            failures);
        ValidateSchedule(
            "Bot:Schedules:ClassicStandings",
            options.Schedules.ClassicStandings,
            failures);
        ValidateSchedule(
            "Bot:Schedules:HeadToHeadStandings",
            options.Schedules.HeadToHeadStandings,
            failures);
        ValidateSchedule(
            "Bot:Schedules:BenchWarmingLeague",
            options.Schedules.BenchWarmingLeague,
            failures);
        ValidateSchedule(
            "Bot:Schedules:FplStatisticsCollection",
            options.Schedules.FplStatisticsCollection,
            failures);
        ValidateSchedule(
            "Bot:Schedules:FplGameweekRecap",
            options.Schedules.FplGameweekRecap,
            failures);
        ValidateSchedule(
            "Bot:Schedules:FplLiveInsights",
            options.Schedules.FplLiveInsights,
            failures);
        ValidateSchedule(
            "Bot:Schedules:FplPriceChanges",
            options.Schedules.FplPriceChanges,
            failures);

        if (requireOperationalConfiguration && !options.Schedules.HasEnabledJobs)
        {
            failures.Add(
                "Production configuration must enable at least one job under Bot:Schedules.");
        }

        if (options.Schedules.ClassicStandings.Enabled &&
            options.FantasyPremierLeague.ClassicLeagueId <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:ClassicLeagueId is required when the classic standings job is enabled.");
        }

        if (options.Schedules.HeadToHeadStandings.Enabled &&
            options.FantasyPremierLeague.HeadToHeadLeagueId <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:HeadToHeadLeagueId is required when the head-to-head standings job is enabled.");
        }

        if (options.Schedules.BenchWarmingLeague.Enabled &&
            options.FantasyPremierLeague.ClassicLeagueId <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:ClassicLeagueId is required when the bench warming league job is enabled.");
        }

        if (options.Schedules.FplStatisticsCollection.Enabled &&
            options.FantasyPremierLeague.ClassicLeagueId <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:ClassicLeagueId is required when the historical FPL statistics collection job is enabled.");
        }

        if (options.Schedules.FplGameweekRecap.Enabled &&
            options.FantasyPremierLeague.ClassicLeagueId <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:ClassicLeagueId is required when the FPL gameweek recap job is enabled.");
        }

        if (options.Schedules.FplLiveInsights.Enabled &&
            options.FantasyPremierLeague.ClassicLeagueId <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:ClassicLeagueId is required when the FPL live insights job is enabled.");
        }

        if (options.FantasyPremierLeague.MaxStandingsPages <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:MaxStandingsPages must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.FantasyPremierLeague.RecognitionRuleVersion))
        {
            failures.Add(
                "Bot:FantasyPremierLeague:RecognitionRuleVersion is required.");
        }

        if (options.FantasyPremierLeague.LargeBenchPointsThreshold <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:LargeBenchPointsThreshold must be greater than zero.");
        }

        if (options.FantasyPremierLeague.CaptainSuccessEffectivePointsThreshold <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:CaptainSuccessEffectivePointsThreshold must be greater than zero.");
        }

        if (options.FantasyPremierLeague.CaptainDisasterPointsThreshold < 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:CaptainDisasterPointsThreshold cannot be negative.");
        }

        if (options.FantasyPremierLeague.CaptainDisasterViceCaptainPointsThreshold <=
            options.FantasyPremierLeague.CaptainDisasterPointsThreshold)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:CaptainDisasterViceCaptainPointsThreshold must be greater than CaptainDisasterPointsThreshold.");
        }

        if (options.FantasyPremierLeague.TransferCostAchievementThreshold <= 0)
        {
            failures.Add(
                "Bot:FantasyPremierLeague:TransferCostAchievementThreshold must be greater than zero.");
        }

        ValidateTargets(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateTimeZone(string timeZoneId, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            failures.Add("Bot:Schedules:TimeZoneId is required.");
            return;
        }

        try
        {
            _ = TZConvert.GetTimeZoneInfo(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            failures.Add(
                $"Bot:Schedules:TimeZoneId '{timeZoneId}' is not a recognized time zone.");
        }
        catch (InvalidTimeZoneException)
        {
            failures.Add(
                $"Bot:Schedules:TimeZoneId '{timeZoneId}' is invalid on this host.");
        }
    }

    private static void ValidateSchedule(
        string path,
        ScheduledJobOptions schedule,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(schedule.Cron) ||
            !CronExpression.IsValidExpression(schedule.Cron))
        {
            failures.Add($"{path}:Cron must be a valid Quartz cron expression.");
        }
    }

    private static void ValidateTargets(
        MandarinBotOptions options,
        List<string> failures)
    {
        if (options.Schedules.HasEnabledNotificationJobs && options.Notifications.Targets.Count == 0)
        {
            failures.Add(
                "Bot:Notifications:Targets must contain at least one explicit guild/channel target when a job is enabled.");
        }

        var configuredTargets = new HashSet<(ulong GuildId, ulong ChannelId)>();

        for (var index = 0; index < options.Notifications.Targets.Count; index++)
        {
            var target = options.Notifications.Targets[index];
            var path = $"Bot:Notifications:Targets:{index}";

            if (target.GuildId == 0)
            {
                failures.Add($"{path}:GuildId must contain a Discord guild ID.");
            }

            if (target.ChannelId == 0)
            {
                failures.Add($"{path}:ChannelId must contain a Discord channel ID.");
            }

            if (!configuredTargets.Add((target.GuildId, target.ChannelId)))
            {
                failures.Add($"{path} duplicates an earlier guild/channel target.");
            }
        }
    }
}
