using AwesomeAssertions;
using DiscordBot.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace DiscordBot.Tests;

[TestFixture]
public sealed class MandarinBotOptionsTests
{
    [Test]
    public void Defaults_NoExplicitConfiguration_CannotScheduleOrBroadcast()
    {
        // Arrange
        var options = new MandarinBotOptions();

        // Act
        var hasEnabledJobs = options.Schedules.HasEnabledJobs;

        // Assert
        hasEnabledJobs.Should().BeFalse();
        options.Notifications.Targets.Should().BeEmpty();
        options.Discord.Commands.RegistrationMode.Should()
            .Be(DiscordCommandRegistrationMode.Disabled);
        options.WelcomeMessages.Enabled.Should().BeFalse();
        new NotificationTargetOptions().MentionEveryone.Should().BeFalse();
        options.Schedules.FplGameweekRecap.Enabled.Should().BeFalse();
        options.Schedules.FplLiveInsights.Enabled.Should().BeFalse();
        options.FantasyPremierLeague.MaxStandingsPages.Should().Be(
            FantasyPremierLeagueOptions.DefaultMaxStandingsPages);
    }

    [Test]
    public async Task ValidateOnStart_MissingProductionConfiguration_FailsWithActionablePaths()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bot:Schedules:TimeZoneId"] = "Europe/Riga",
            ["Bot:Schedules:PremierLeagueNotifications:Cron"] = "0 0 * * * ?",
            ["Bot:Schedules:UclFantasyNotifications:Cron"] = "0 0 * * * ?",
            ["Bot:Schedules:ClassicStandings:Cron"] = "0 0 17 * * ?",
            ["Bot:Schedules:HeadToHeadStandings:Cron"] = "0 0 17 * * ?",
            ["Bot:Schedules:BenchWarmingLeague:Cron"] = "0 0 18 * * ?",
            ["Bot:Schedules:FplStatisticsCollection:Cron"] = "0 0 19 * * ?",
            ["Bot:Schedules:FplGameweekRecap:Cron"] = "0 0 20 * * ?",
            ["Bot:Schedules:FplLiveInsights:Cron"] = "0 0/15 * * * ?"
        });
        builder.Services.AddMandarinBotConfiguration(
            builder.Configuration,
            builder.Environment);
        using var host = builder.Build();

        // Act
        Func<Task> start = () => host.StartAsync();

        // Assert
        var exception = await start.Should().ThrowAsync<OptionsValidationException>();
        exception.Which.Failures.Should().Contain(failure =>
            failure.Contains("Bot:Discord:Token"));
        exception.Which.Failures.Should().Contain(failure =>
            failure.Contains("enable at least one job"));
    }

    [Test]
    public void Validate_EnabledClassicJobWithoutLeagueOrTarget_ReturnsBothFailures()
    {
        // Arrange
        var options = CreateValidOptions(
            leagueOptions: new FantasyPremierLeagueOptions(),
            schedules: new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                UclFantasyNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                ClassicStandings = ValidSchedule(enabled: true, "0 0 17 * * ?"),
                HeadToHeadStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                BenchWarmingLeague = ValidSchedule(enabled: false, "0 0 18 * * ?"),
                FplStatisticsCollection = ValidSchedule(enabled: false, "0 0 19 * * ?"),
                FplGameweekRecap = ValidSchedule(enabled: false, "0 0 20 * * ?"),
                FplLiveInsights = ValidSchedule(enabled: false, "0 0/15 * * * ?")
            },
            targets: []);
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure =>
            failure.Contains("ClassicLeagueId"));
        result.Failures.Should().Contain(failure =>
            failure.Contains("Targets"));
    }

    [Test]
    public void Validate_EnabledBenchWarmingJobWithoutClassicLeague_ReturnsFailure()
    {
        // Arrange
        var options = CreateValidOptions(
            leagueOptions: new FantasyPremierLeagueOptions
            {
                ClassicLeagueId = 0,
                HeadToHeadLeagueId = 456
            },
            schedules: new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                UclFantasyNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                ClassicStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                HeadToHeadStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                BenchWarmingLeague = ValidSchedule(enabled: true, "0 0 18 * * ?"),
                FplStatisticsCollection = ValidSchedule(enabled: false, "0 0 19 * * ?"),
                FplGameweekRecap = ValidSchedule(enabled: false, "0 0 20 * * ?"),
                FplLiveInsights = ValidSchedule(enabled: false, "0 0/15 * * * ?")
            });
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure =>
            failure.Contains("bench warming league job is enabled"));
    }

    [Test]
    public void Validate_EnabledGameweekRecapWithoutClassicLeague_ReturnsFailure()
    {
        // Arrange
        var options = CreateValidOptions(
            leagueOptions: new FantasyPremierLeagueOptions
            {
                ClassicLeagueId = 0,
                HeadToHeadLeagueId = 456
            },
            schedules: new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                UclFantasyNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                ClassicStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                HeadToHeadStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                BenchWarmingLeague = ValidSchedule(enabled: false, "0 0 18 * * ?"),
                FplStatisticsCollection = ValidSchedule(enabled: false, "0 0 19 * * ?"),
                FplGameweekRecap = ValidSchedule(enabled: true, "0 0 20 * * ?"),
                FplLiveInsights = ValidSchedule(enabled: false, "0 0/15 * * * ?")
            });
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure =>
            failure.Contains("FPL gameweek recap job is enabled"));
    }

    [Test]
    public void Validate_EnabledLiveInsightsWithoutClassicLeague_ReturnsFailure()
    {
        // Arrange
        var options = CreateValidOptions(
            leagueOptions: new FantasyPremierLeagueOptions
            {
                ClassicLeagueId = 0,
                HeadToHeadLeagueId = 456
            },
            schedules: new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                UclFantasyNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                ClassicStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                HeadToHeadStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                BenchWarmingLeague = ValidSchedule(enabled: false, "0 0 18 * * ?"),
                FplStatisticsCollection = ValidSchedule(enabled: false, "0 0 19 * * ?"),
                FplGameweekRecap = ValidSchedule(enabled: false, "0 0 20 * * ?"),
                FplLiveInsights = ValidSchedule(enabled: true, "0 0/15 * * * ?")
            });
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure =>
            failure.Contains("FPL live insights job is enabled"));
    }

    [Test]
    public void Validate_InvalidLiveInsightThresholds_ReturnsActionableFailures()
    {
        // Arrange
        var options = CreateValidOptions(
            leagueOptions: new FantasyPremierLeagueOptions
            {
                LargeBenchPointsThreshold = 0,
                CaptainSuccessEffectivePointsThreshold = 0,
                CaptainDisasterPointsThreshold = 10,
                CaptainDisasterViceCaptainPointsThreshold = 10
            });
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure =>
            failure.Contains("LargeBenchPointsThreshold"));
        result.Failures.Should().Contain(failure =>
            failure.Contains("CaptainSuccessEffectivePointsThreshold"));
        result.Failures.Should().Contain(failure =>
            failure.Contains("CaptainDisasterViceCaptainPointsThreshold"));
    }

    [Test]
    public void Validate_MultipleDistinctTargets_ReturnsSuccess()
    {
        // Arrange
        var options = CreateValidOptions(
            targets:
            [
                new NotificationTargetOptions
                {
                    GuildId = 100,
                    ChannelId = 101
                },
                new NotificationTargetOptions
                {
                    GuildId = 200,
                    ChannelId = 201,
                    MentionEveryone = true
                }
            ]);
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void Validate_EnabledHistoricalCollectionWithoutNotificationTarget_ReturnsSuccess()
    {
        // Arrange
        var options = CreateValidOptions(
            schedules: new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                UclFantasyNotifications = ValidSchedule(enabled: false, "0 0 * * * ?"),
                ClassicStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                HeadToHeadStandings = ValidSchedule(enabled: false, "0 0 17 * * ?"),
                BenchWarmingLeague = ValidSchedule(enabled: false, "0 0 18 * * ?"),
                FplStatisticsCollection = ValidSchedule(enabled: true, "0 0 19 * * ?"),
                FplGameweekRecap = ValidSchedule(enabled: false, "0 0 20 * * ?"),
                FplLiveInsights = ValidSchedule(enabled: false, "0 0/15 * * * ?")
            },
            targets: []);
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
        options.Schedules.HasEnabledJobs.Should().BeTrue();
        options.Schedules.HasEnabledNotificationJobs.Should().BeFalse();
    }

    [Test]
    public void Validate_DuplicateGuildAndChannelTarget_ReturnsFailure()
    {
        // Arrange
        var options = CreateValidOptions(
            targets:
            [
                new NotificationTargetOptions { GuildId = 100, ChannelId = 101 },
                new NotificationTargetOptions { GuildId = 100, ChannelId = 101 }
            ]);
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("duplicates"));
    }

    [Test]
    public void Validate_NonPositiveStandingsPageLimit_ReturnsFailure()
    {
        // Arrange
        var options = CreateValidOptions(
            leagueOptions: new FantasyPremierLeagueOptions
            {
                ClassicLeagueId = 123,
                HeadToHeadLeagueId = 456,
                MaxStandingsPages = 0
            });
        var validator = new MandarinBotOptionsValidator(
            requireOperationalConfiguration: true);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure =>
            failure.Contains("MaxStandingsPages"));
    }

    [Test]
    public void Bind_IndexedEnvironmentStyleTargets_PreservesIndependentSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bot:Notifications:Targets:0:GuildId"] = "100",
                ["Bot:Notifications:Targets:0:ChannelId"] = "101",
                ["Bot:Notifications:Targets:0:MentionEveryone"] = "false",
                ["Bot:Notifications:Targets:1:GuildId"] = "200",
                ["Bot:Notifications:Targets:1:ChannelId"] = "201",
                ["Bot:Notifications:Targets:1:MentionEveryone"] = "true"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(MandarinBotOptions.SectionName)
            .Get<MandarinBotOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Notifications.Targets.Should().HaveCount(2);
        options.Notifications.Targets[0].MentionEveryone.Should().BeFalse();
        options.Notifications.Targets[1].Should().BeEquivalentTo(
            new NotificationTargetOptions
            {
                GuildId = 200,
                ChannelId = 201,
                MentionEveryone = true
            });
    }

    [TestCase(false, "League update")]
    [TestCase(true, "@everyone League update")]
    public void FormatMessage_MentionEveryoneSetting_UsesPerTargetOptIn(
        bool mentionEveryone,
        string expected)
    {
        // Arrange
        var target = new NotificationTargetOptions
        {
            GuildId = 100,
            ChannelId = 101,
            MentionEveryone = mentionEveryone
        };

        // Act
        var message = target.FormatMessage("League update");

        // Assert
        message.Should().Be(expected);
    }

    private static MandarinBotOptions CreateValidOptions(
        FantasyPremierLeagueOptions? leagueOptions = null,
        JobSchedulesOptions? schedules = null,
        List<NotificationTargetOptions>? targets = null)
    {
        return new MandarinBotOptions
        {
            Discord = new DiscordOptions
            {
                Token = "test-token",
                ReadinessTimeout = TimeSpan.FromSeconds(30)
            },
            FantasyPremierLeague = leagueOptions ??
                new FantasyPremierLeagueOptions
                {
                    ClassicLeagueId = 123,
                    HeadToHeadLeagueId = 456
                },
            Schedules = schedules ?? new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = ValidSchedule(enabled: true, "0 0 * * * ?"),
                UclFantasyNotifications = ValidSchedule(enabled: true, "0 0 * * * ?"),
                ClassicStandings = ValidSchedule(enabled: true, "0 0 17 * * ?"),
                HeadToHeadStandings = ValidSchedule(enabled: true, "0 0 17 * * ?"),
                BenchWarmingLeague = ValidSchedule(enabled: true, "0 0 18 * * ?"),
                FplStatisticsCollection = ValidSchedule(enabled: false, "0 0 19 * * ?"),
                FplGameweekRecap = ValidSchedule(enabled: false, "0 0 20 * * ?"),
                FplLiveInsights = ValidSchedule(enabled: false, "0 0/15 * * * ?")
            },
            Notifications = new NotificationOptions
            {
                Targets = targets ??
                [
                    new NotificationTargetOptions
                    {
                        GuildId = 100,
                        ChannelId = 101
                    }
                ]
            }
        };
    }

    private static ScheduledJobOptions ValidSchedule(bool enabled, string cron)
    {
        return new ScheduledJobOptions
        {
            Enabled = enabled,
            Cron = cron
        };
    }
}
