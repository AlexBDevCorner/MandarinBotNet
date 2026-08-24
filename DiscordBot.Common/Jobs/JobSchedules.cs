using Quartz;
using TimeZoneConverter;

namespace DiscordBot.Jobs;

public static class JobSchedules
{
    public const string PremierLeagueNotificationTriggerName =
        "PremierLeagueNotificationTrigger";
    public const string UclDeadlineNotificationTriggerName =
        "UclDeadlineNotificationTrigger";
    public const string PremierLeagueClassicStandingsInformationTriggerName =
        "PremierLeagueClassicStandingsInformationTrigger";
    public const string PremierLeagueH2hStandingsInformationTriggerName =
        "PremierLeagueH2hStandingsInformationTrigger";
    public const string BenchWarmingLeagueCalculationTriggerName =
        "BenchWarmingLeagueCalculationTrigger";
    public const string FplStatisticsCollectionTriggerName =
        "FplStatisticsCollectionTrigger";
    public const string FplGameweekRecapTriggerName =
        "FplGameweekRecapTrigger";
    public const string FplLiveInsightsTriggerName =
        "FplLiveInsightsTrigger";

    public static readonly JobKey PremierLeagueNotificationJobKey =
        new("PremierLeagueNotification");

    public static readonly JobKey UclDeadlineNotificationJobKey =
        new("UclDeadlineNotification");

    public static readonly JobKey PremierLeagueClassicStandingsInformationJobKey =
        new("PremierLeagueClassicStandingsInformation");

    public static readonly JobKey PremierLeagueH2hStandingsInformationJobKey =
        new("PremierLeagueH2hStandingsInformation");

    public static readonly JobKey BenchWarmingLeagueCalculationJobKey =
        new("BenchWarmingLeagueCalculation");

    public static readonly JobKey FplStatisticsCollectionJobKey =
        new("FplStatisticsCollection");

    public static readonly JobKey FplGameweekRecapJobKey =
        new("FplGameweekRecap");

    public static readonly JobKey FplLiveInsightsJobKey =
        new("FplLiveInsights");

    public static TimeZoneInfo GetTimeZone(JobSchedulesOptions options)
    {
        return TZConvert.GetTimeZoneInfo(options.TimeZoneId);
    }

    public static ITrigger CreatePremierLeagueNotificationTrigger(
        JobSchedulesOptions options) =>
        CreateCronTrigger(
            PremierLeagueNotificationJobKey,
            PremierLeagueNotificationTriggerName,
            options.PremierLeagueNotifications.Cron,
            options.TimeZoneId);

    public static ITrigger CreateUclDeadlineNotificationTrigger(
        JobSchedulesOptions options) =>
        CreateCronTrigger(
            UclDeadlineNotificationJobKey,
            UclDeadlineNotificationTriggerName,
            options.UclFantasyNotifications.Cron,
            options.TimeZoneId);

    public static ITrigger CreatePremierLeagueClassicStandingsInformationTrigger(
        JobSchedulesOptions options) =>
        CreateCronTrigger(
            PremierLeagueClassicStandingsInformationJobKey,
            PremierLeagueClassicStandingsInformationTriggerName,
            options.ClassicStandings.Cron,
            options.TimeZoneId);

    public static ITrigger CreatePremierLeagueH2hStandingsInformationTrigger(
        JobSchedulesOptions options) =>
        CreateCronTrigger(
            PremierLeagueH2hStandingsInformationJobKey,
            PremierLeagueH2hStandingsInformationTriggerName,
            options.HeadToHeadStandings.Cron,
            options.TimeZoneId);

    public static ITrigger CreateBenchWarmingLeagueCalculationTrigger(
        JobSchedulesOptions options) =>
        CreateCronTrigger(
            BenchWarmingLeagueCalculationJobKey,
            BenchWarmingLeagueCalculationTriggerName,
            options.BenchWarmingLeague.Cron,
            options.TimeZoneId);

    public static ITrigger CreateFplStatisticsCollectionTrigger(
        JobSchedulesOptions options) =>
        CreateCronTrigger(
            FplStatisticsCollectionJobKey,
            FplStatisticsCollectionTriggerName,
            options.FplStatisticsCollection.Cron,
            options.TimeZoneId);

    public static ITrigger CreateFplGameweekRecapTrigger(
        JobSchedulesOptions options) =>
        CreateCronTrigger(
            FplGameweekRecapJobKey,
            FplGameweekRecapTriggerName,
            options.FplGameweekRecap.Cron,
            options.TimeZoneId);

    public static ITrigger CreateFplLiveInsightsTrigger(
        JobSchedulesOptions options) =>
        CreateCronTrigger(
            FplLiveInsightsJobKey,
            FplLiveInsightsTriggerName,
            options.FplLiveInsights.Cron,
            options.TimeZoneId);

    public static IReadOnlyList<ITrigger> CreateAllTriggers(
        JobSchedulesOptions options) =>
    [
        CreatePremierLeagueNotificationTrigger(options),
        CreateUclDeadlineNotificationTrigger(options),
        CreatePremierLeagueClassicStandingsInformationTrigger(options),
        CreatePremierLeagueH2hStandingsInformationTrigger(options),
        CreateBenchWarmingLeagueCalculationTrigger(options),
        CreateFplStatisticsCollectionTrigger(options),
        CreateFplGameweekRecapTrigger(options),
        CreateFplLiveInsightsTrigger(options)
    ];

    private static ITrigger CreateCronTrigger(
        JobKey jobKey,
        string triggerName,
        string cronExpression,
        string timeZoneId)
    {
        return TriggerBuilder.Create()
            .ForJob(jobKey)
            .WithIdentity(triggerName)
            .WithCronSchedule(
                cronExpression,
                schedule => schedule
                    .InTimeZone(TZConvert.GetTimeZoneInfo(timeZoneId))
                    .WithMisfireHandlingInstructionDoNothing())
            .Build();
    }
}
