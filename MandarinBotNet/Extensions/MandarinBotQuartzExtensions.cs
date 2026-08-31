using DiscordBot;
using DiscordBot.Jobs;
using DiscordBot.UclFantasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace MandarinBotNet.Extensions;

public static class MandarinBotQuartzExtensions
{
    public static IServiceCollection AddMandarinBotScheduling(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddQuartz();
        services.AddOptions<QuartzOptions>()
            .Configure<IOptions<MandarinBotOptions>>(
                (quartzOptions, botOptions) => ConfigureScheduledJobs(
                    quartzOptions,
                    botOptions.Value.Schedules));
        services.AddQuartzHostedService(options =>
            options.WaitForJobsToComplete = true);

        return services;
    }

    private static void ConfigureScheduledJobs(
        QuartzOptions quartzOptions,
        JobSchedulesOptions schedules)
    {
        var timeZone = JobSchedules.GetTimeZone(schedules);

        if (schedules.PremierLeagueNotifications.Enabled)
        {
            AddScheduledJob<PremierLeagueNotificationJob>(
                quartzOptions,
                JobSchedules.PremierLeagueNotificationJobKey,
                JobSchedules.PremierLeagueNotificationTriggerName,
                schedules.PremierLeagueNotifications.Cron,
                timeZone);
        }

        if (schedules.UclFantasyNotifications.Enabled)
        {
            AddScheduledJob<UclDeadlineNotificationJob>(
                quartzOptions,
                JobSchedules.UclDeadlineNotificationJobKey,
                JobSchedules.UclDeadlineNotificationTriggerName,
                schedules.UclFantasyNotifications.Cron,
                timeZone);
        }

        if (schedules.ClassicStandings.Enabled)
        {
            AddScheduledJob<PremierLeagueClassicStandingsInformationJob>(
                quartzOptions,
                JobSchedules.PremierLeagueClassicStandingsInformationJobKey,
                JobSchedules.PremierLeagueClassicStandingsInformationTriggerName,
                schedules.ClassicStandings.Cron,
                timeZone);
        }

        if (schedules.HeadToHeadStandings.Enabled)
        {
            AddScheduledJob<PremierLeagueH2hStandingsInformationJob>(
                quartzOptions,
                JobSchedules.PremierLeagueH2hStandingsInformationJobKey,
                JobSchedules.PremierLeagueH2hStandingsInformationTriggerName,
                schedules.HeadToHeadStandings.Cron,
                timeZone);
        }

        if (schedules.BenchWarmingLeague.Enabled)
        {
            AddScheduledJob<BenchWarmingLeagueCalculationJob>(
                quartzOptions,
                JobSchedules.BenchWarmingLeagueCalculationJobKey,
                JobSchedules.BenchWarmingLeagueCalculationTriggerName,
                schedules.BenchWarmingLeague.Cron,
                timeZone);
        }

        if (schedules.FplStatisticsCollection.Enabled)
        {
            AddScheduledJob<FplStatisticsCollectionJob>(
                quartzOptions,
                JobSchedules.FplStatisticsCollectionJobKey,
                JobSchedules.FplStatisticsCollectionTriggerName,
                schedules.FplStatisticsCollection.Cron,
                timeZone);
        }

        if (schedules.FplGameweekRecap.Enabled)
        {
            AddScheduledJob<FplGameweekRecapJob>(
                quartzOptions,
                JobSchedules.FplGameweekRecapJobKey,
                JobSchedules.FplGameweekRecapTriggerName,
                schedules.FplGameweekRecap.Cron,
                timeZone);
        }

        if (schedules.FplLiveInsights.Enabled)
        {
            AddScheduledJob<FplLiveInsightsJob>(
                quartzOptions,
                JobSchedules.FplLiveInsightsJobKey,
                JobSchedules.FplLiveInsightsTriggerName,
                schedules.FplLiveInsights.Cron,
                timeZone);
        }

        if (schedules.FplPriceChanges.Enabled)
        {
            AddScheduledJob<FplPriceChangeJob>(
                quartzOptions,
                JobSchedules.FplPriceChangesJobKey,
                JobSchedules.FplPriceChangesTriggerName,
                schedules.FplPriceChanges.Cron,
                timeZone);
        }

        if (schedules.FplChipWatch.Enabled)
        {
            AddScheduledJob<FplChipWatchJob>(
                quartzOptions,
                JobSchedules.FplChipWatchJobKey,
                JobSchedules.FplChipWatchTriggerName,
                schedules.FplChipWatch.Cron,
                timeZone);
        }
    }

    private static void AddScheduledJob<TJob>(
        QuartzOptions quartzOptions,
        JobKey jobKey,
        string triggerName,
        string cron,
        TimeZoneInfo timeZone)
        where TJob : IJob
    {
        quartzOptions.AddJob<TJob>(job => job.WithIdentity(jobKey));
        quartzOptions.AddTrigger(trigger => trigger
            .ForJob(jobKey)
            .WithIdentity(triggerName)
            .WithCronSchedule(
                cron,
                schedule => schedule
                    .InTimeZone(timeZone)
                    .WithMisfireHandlingInstructionDoNothing()));
    }
}
