using AwesomeAssertions;
using DiscordBot.EventWatch;
using DiscordBot.Jobs;
using MandarinBotNet.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class EventWatchSchedulingTests
{
    [Test]
    public void CreateEventWatchTrigger_DefaultCron_RunsEveryTenMinutesInRiga()
    {
        var options = new JobSchedulesOptions
        {
            TimeZoneId = "Europe/Riga",
            EventWatch = new ScheduledJobOptions
            {
                Enabled = false,
                Cron = "0 0/10 * * * ?"
            }
        };

        var trigger = JobSchedules.CreateEventWatchTrigger(options);

        var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
        cronTrigger.CronExpressionString.Should().Be("0 0/10 * * * ?");
        cronTrigger.TimeZone.Should().Be(JobSchedules.GetTimeZone(options));
        trigger.MisfireInstruction.Should().Be(MisfireInstruction.CronTrigger.DoNothing);
    }

    [Test]
    public void JobType_EventWatch_DisallowsConcurrentExecution()
    {
        var attribute = Attribute.GetCustomAttribute(
            typeof(EventWatchJob),
            typeof(DisallowConcurrentExecutionAttribute));

        attribute.Should().NotBeNull();
    }

    [Test]
    public void Scheduling_Disabled_DoesNotRegisterEventWatchJob()
    {
        using var provider = CreateSchedulingProvider(enabled: false);
        var quartzOptions = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        quartzOptions.JobDetails.Should().NotContain(j => j.Key.Equals(JobSchedules.EventWatchJobKey));
        quartzOptions.Triggers.Should().NotContain(t => t.JobKey.Equals(JobSchedules.EventWatchJobKey));
    }

    [Test]
    public void Scheduling_Enabled_RegistersEventWatchJobWithTenMinuteCron()
    {
        using var provider = CreateSchedulingProvider(enabled: true);
        var quartzOptions = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        quartzOptions.JobDetails.Should().ContainSingle(j => j.Key.Equals(JobSchedules.EventWatchJobKey));
        var trigger = quartzOptions.Triggers.Should().ContainSingle(t => t.JobKey.Equals(JobSchedules.EventWatchJobKey)).Subject;
        var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
        cronTrigger.CronExpressionString.Should().Be("0 0/10 * * * ?");
    }

    private static ServiceProvider CreateSchedulingProvider(bool enabled)
    {
        var botOptions = new MandarinBotOptions
        {
            Discord = new DiscordOptions
            {
                Token = "test-token",
                ReadinessTimeout = TimeSpan.FromSeconds(30)
            },
            FantasyPremierLeague = new FantasyPremierLeagueOptions
            {
                ClassicLeagueId = 123,
                HeadToHeadLeagueId = 456
            },
            Schedules = new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                UclFantasyNotifications = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                ClassicStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                HeadToHeadStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                BenchWarmingLeague = new ScheduledJobOptions { Enabled = false, Cron = "0 0 18 * * ?" },
                FplStatisticsCollection = new ScheduledJobOptions { Enabled = false, Cron = "0 0 19 * * ?" },
                FplGameweekRecap = new ScheduledJobOptions { Enabled = false, Cron = "0 0 20 * * ?" },
                FplLiveInsights = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                FplPriceChanges = new ScheduledJobOptions { Enabled = false, Cron = "0 0 12-22 * * ?" },
                FplChipWatch = new ScheduledJobOptions { Enabled = false, Cron = "0 0 6,12,18 * * ?" },
                EventWatch = new ScheduledJobOptions { Enabled = enabled, Cron = "0 0/10 * * * ?" }
            },
            Notifications = new NotificationOptions
            {
                Targets = []
            },
            EventWatch = new EventWatchOptions
            {
                Watches =
                [
                    new EventWatchDefinition
                    {
                        Enabled = enabled,
                        Id = "riga-fc-atalanta-2026",
                        Title = "Riga FC vs Atalanta tickets",
                        MatchTerms = ["Atalanta"],
                        Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
                    }
                ]
            },
            AutonomousWorkDispatcher = new AutonomousWorkDispatcherOptions()
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(botOptions));
        services.AddSingleton(botOptions.Schedules);
        services.AddSingleton(botOptions.AutonomousWorkDispatcher);
        services.AddSingleton(TimeProvider.System);
        services.AddMandarinBotScheduling();
        return services.BuildServiceProvider();
    }
}
