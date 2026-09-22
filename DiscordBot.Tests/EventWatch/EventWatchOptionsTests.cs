using AwesomeAssertions;
using DiscordBot.Notifications;
using NUnit.Framework;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class EventWatchOptionsTests
{
    [Test]
    public void Defaults_DisabledWithExpectedCronAndAtalantaWatch()
    {
        var options = new MandarinBotOptions();

        options.Schedules.EventWatch.Enabled.Should().BeFalse();
        options.Schedules.EventWatch.Cron.Should().Be("0 0/10 * * * ?");
        options.Schedules.HasEnabledJobs.Should().BeFalse();

        // Repository defaults are provided via appsettings, not via the
        // parameterless options: the code default keeps watches empty so a
        // checkout cannot notify without explicit configuration.
        options.EventWatch.Watches.Should().BeEmpty();
    }

    [Test]
    public void Validate_EventWatchDisabledWithPlaceholderTargets_ReturnsSuccess()
    {
        var options = CreateValidOptions(
            eventWatchEnabled: false,
            watches:
            [
                new EventWatchDefinition
                {
                    Enabled = false,
                    Id = "riga-fc-atalanta-2026",
                    Title = "Riga FC vs Atalanta tickets",
                    MatchTerms = ["Atalanta"],
                    Targets =
                    [
                        new NotificationTargetOptions
                        {
                            GuildId = 0,
                            ChannelId = 0,
                            MentionEveryone = true
                        }
                    ]
                }
            ]);

        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void Validate_EventWatchEnabledWithoutEnabledWatch_ReturnsFailure()
    {
        var options = CreateValidOptions(
            eventWatchEnabled: true,
            watches:
            [
                new EventWatchDefinition
                {
                    Enabled = false,
                    Id = "riga-fc-atalanta-2026",
                    Title = "Riga FC vs Atalanta tickets",
                    MatchTerms = ["Atalanta"],
                    Targets =
                    [
                        new NotificationTargetOptions { GuildId = 1, ChannelId = 2 }
                    ]
                }
            ]);

        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("at least one enabled watch"));
    }

    [Test]
    public void Validate_EnabledWatchWithoutTargets_ReturnsFailure()
    {
        var options = CreateValidOptions(
            eventWatchEnabled: true,
            watches:
            [
                new EventWatchDefinition
                {
                    Enabled = true,
                    Id = "riga-fc-atalanta-2026",
                    Title = "Riga FC vs Atalanta tickets",
                    MatchTerms = ["Atalanta"],
                    Targets = []
                }
            ]);

        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(":Targets must contain"));
    }

    [Test]
    public void Validate_EnabledWatchWithZeroIds_ReturnsFailures()
    {
        var options = CreateValidOptions(
            eventWatchEnabled: true,
            watches:
            [
                new EventWatchDefinition
                {
                    Enabled = true,
                    Id = "riga-fc-atalanta-2026",
                    Title = "Riga FC vs Atalanta tickets",
                    MatchTerms = ["Atalanta"],
                    Targets =
                    [
                        new NotificationTargetOptions { GuildId = 0, ChannelId = 0 }
                    ]
                }
            ]);

        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("GuildId"));
        result.Failures.Should().Contain(f => f.Contains("ChannelId"));
    }

    [Test]
    public void Validate_DuplicateWatchIds_ReturnsFailure()
    {
        var options = CreateValidOptions(
            eventWatchEnabled: true,
            watches:
            [
                new EventWatchDefinition
                {
                    Enabled = true,
                    Id = "riga-fc-atalanta-2026",
                    Title = "First",
                    MatchTerms = ["Atalanta"],
                    Targets = [new NotificationTargetOptions { GuildId = 1, ChannelId = 2 }]
                },
                new EventWatchDefinition
                {
                    Enabled = true,
                    Id = "riga-fc-atalanta-2026",
                    Title = "Second",
                    MatchTerms = ["Atalanta"],
                    Targets = [new NotificationTargetOptions { GuildId = 3, ChannelId = 4 }]
                }
            ]);

        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("duplicates"));
    }

    [Test]
    public void Validate_EmptyTitleOrMatchTerms_ReturnsFailures()
    {
        var options = CreateValidOptions(
            eventWatchEnabled: true,
            watches:
            [
                new EventWatchDefinition
                {
                    Enabled = true,
                    Id = "riga-fc-atalanta-2026",
                    Title = "",
                    MatchTerms = [],
                    Targets = [new NotificationTargetOptions { GuildId = 1, ChannelId = 2 }]
                }
            ]);

        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(":Title"));
        result.Failures.Should().Contain(f => f.Contains("MatchTerms"));
    }

    [Test]
    public void Validate_InvalidEventWatchCron_ReturnsFailure()
    {
        var options = CreateValidOptions(
            eventWatchEnabled: true,
            eventWatchCron: "not-a-cron",
            watches:
            [
                new EventWatchDefinition
                {
                    Enabled = true,
                    Id = "riga-fc-atalanta-2026",
                    Title = "Riga FC vs Atalanta tickets",
                    MatchTerms = ["Atalanta"],
                    Targets = [new NotificationTargetOptions { GuildId = 1, ChannelId = 2 }]
                }
            ]);

        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Bot:Schedules:EventWatch"));
    }

    [Test]
    public void Validate_ValidEnabledWatch_ReturnsSuccess()
    {
        var options = CreateValidOptions(
            eventWatchEnabled: true,
            watches:
            [
                new EventWatchDefinition
                {
                    Enabled = true,
                    Id = "riga-fc-atalanta-2026",
                    Title = "Riga FC vs Atalanta tickets",
                    MatchTerms = ["Atalanta"],
                    Targets =
                    [
                        new NotificationTargetOptions { GuildId = 10, ChannelId = 100, MentionEveryone = true }
                    ]
                }
            ]);

        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [TestCase(NotificationTypes.EventWatchAnnouncement, true)]
    [TestCase(NotificationTypes.EventWatchTicketLink, true)]
    public void AllowsEveryoneMention_EventWatchTypes_ReturnsTrue(
        string notificationType,
        bool expected)
    {
        NotificationTypes.AllowsEveryoneMention(notificationType).Should().Be(expected);
    }

    private static MandarinBotOptions CreateValidOptions(
        bool eventWatchEnabled,
        List<EventWatchDefinition>? watches = null,
        string eventWatchCron = "0 0/10 * * * ?")
    {
        return new MandarinBotOptions
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
                PremierLeagueNotifications = new ScheduledJobOptions { Enabled = false, Cron = "0 0 * * * ?" },
                UclFantasyNotifications = new ScheduledJobOptions { Enabled = false, Cron = "0 0 * * * ?" },
                ClassicStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                HeadToHeadStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                BenchWarmingLeague = new ScheduledJobOptions { Enabled = false, Cron = "0 0 18 * * ?" },
                FplStatisticsCollection = new ScheduledJobOptions { Enabled = false, Cron = "0 0 19 * * ?" },
                FplGameweekRecap = new ScheduledJobOptions { Enabled = false, Cron = "0 0 20 * * ?" },
                FplLiveInsights = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                FplPriceChanges = new ScheduledJobOptions { Enabled = false, Cron = "0 0 12-22 * * ?" },
                FplChipWatch = new ScheduledJobOptions { Enabled = false, Cron = "0 0 6,12,18 * * ?" },
                EventWatch = new ScheduledJobOptions { Enabled = eventWatchEnabled, Cron = eventWatchCron }
            },
            Notifications = new NotificationOptions
            {
                Targets = []
            },
            EventWatch = new EventWatchOptions
            {
                Watches = watches ?? []
            }
        };
    }
}
