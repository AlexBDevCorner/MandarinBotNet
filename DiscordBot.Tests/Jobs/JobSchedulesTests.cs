using AwesomeAssertions;
using DiscordBot.Jobs;
using DiscordBot.UclFantasy;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.Jobs
{
    [TestFixture]
    public sealed class JobSchedulesTests
    {
        private static readonly JobSchedulesOptions Options = new()
        {
            TimeZoneId = "Europe/Riga",
            PremierLeagueNotifications = new ScheduledJobOptions
            {
                Enabled = true,
                Cron = "0 0 * * * ?"
            },
            UclFantasyNotifications = new ScheduledJobOptions
            {
                Enabled = true,
                Cron = "0 0 * * * ?"
            },
            ClassicStandings = new ScheduledJobOptions
            {
                Enabled = true,
                Cron = "0 0 17 * * ?"
            },
            HeadToHeadStandings = new ScheduledJobOptions
            {
                Enabled = true,
                Cron = "0 0 17 * * ?"
            },
            BenchWarmingLeague = new ScheduledJobOptions
            {
                Enabled = true,
                Cron = "0 0 18 * * ?"
            }
        };

        [Test]
        public void CreatePremierLeagueNotificationTrigger_DefaultSchedule_RunsHourlyInRiga()
        {
            // Arrange
            var expectedTimeZone = JobSchedules.GetTimeZone(Options);

            // Act
            var trigger = JobSchedules.CreatePremierLeagueNotificationTrigger(Options);

            // Assert
            var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
            cronTrigger.CronExpressionString.Should()
                .Be(Options.PremierLeagueNotifications.Cron);
            cronTrigger.TimeZone.Should().Be(expectedTimeZone);
        }

        [Test]
        public void CreateUclDeadlineNotificationTrigger_DefaultSchedule_UsesCronAndRigaTimeZone()
        {
            // Arrange
            var expectedTimeZone = JobSchedules.GetTimeZone(Options);

            // Act
            var trigger = JobSchedules.CreateUclDeadlineNotificationTrigger(Options);

            // Assert
            var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
            cronTrigger.CronExpressionString.Should()
                .Be(Options.UclFantasyNotifications.Cron);
            cronTrigger.TimeZone.Should().Be(expectedTimeZone);
        }

        [Test]
        public void CreateDailyStandingsTrigger_WinterAndSummer_FiresAtConfiguredRigaLocalTime()
        {
            // Arrange
            var trigger = JobSchedules.CreatePremierLeagueClassicStandingsInformationTrigger(
                Options);
            var winterReference = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);
            var summerReference = new DateTimeOffset(2027, 7, 15, 12, 0, 0, TimeSpan.Zero);

            // Act
            var winterFireTime = trigger.GetFireTimeAfter(winterReference);
            var summerFireTime = trigger.GetFireTimeAfter(summerReference);
            var winterRigaTime = TimeZoneInfo.ConvertTime(
                winterFireTime!.Value,
                JobSchedules.GetTimeZone(Options));
            var summerRigaTime = TimeZoneInfo.ConvertTime(
                summerFireTime!.Value,
                JobSchedules.GetTimeZone(Options));

            // Assert
            winterRigaTime.Hour.Should().Be(17);
            summerRigaTime.Hour.Should().Be(17);
            winterRigaTime.Offset.Should().Be(TimeSpan.FromHours(2));
            summerRigaTime.Offset.Should().Be(TimeSpan.FromHours(3));
        }

        [Test]
        public void CreateDailyStandingsTrigger_SpringDstTransition_PreservesRigaLocalTime()
        {
            // Arrange
            var trigger = JobSchedules.CreatePremierLeagueClassicStandingsInformationTrigger(
                Options);
            var reference = new DateTimeOffset(2027, 3, 26, 16, 0, 0, TimeSpan.Zero);

            // Act
            var beforeTransition = trigger.GetFireTimeAfter(reference)!.Value;
            var transitionDay = trigger.GetFireTimeAfter(beforeTransition)!.Value;
            var afterTransition = trigger.GetFireTimeAfter(transitionDay)!.Value;
            var rigaFireTimes = new[] { beforeTransition, transitionDay, afterTransition }
                .Select(time => TimeZoneInfo.ConvertTime(
                    time,
                    JobSchedules.GetTimeZone(Options)))
                .ToArray();

            // Assert
            rigaFireTimes.Should().OnlyContain(time => time.Hour == 17);
            (transitionDay - beforeTransition).Should().Be(TimeSpan.FromHours(23));
            (afterTransition - transitionDay).Should().Be(TimeSpan.FromHours(24));
        }

        [Test]
        public void CreateBenchWarmingLeagueTrigger_ConfiguredSchedule_UsesCronAndRigaTimeZone()
        {
            // Arrange
            var expectedTimeZone = JobSchedules.GetTimeZone(Options);

            // Act
            var trigger = JobSchedules.CreateBenchWarmingLeagueCalculationTrigger(
                Options);

            // Assert
            var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
            cronTrigger.CronExpressionString.Should()
                .Be(Options.BenchWarmingLeague.Cron);
            cronTrigger.TimeZone.Should().Be(expectedTimeZone);
        }

        [Test]
        public void CreateAllTriggers_DefaultSchedules_SkipMissedRuns()
        {
            // Arrange
            var expectedInstruction = MisfireInstruction.CronTrigger.DoNothing;

            // Act
            var triggers = JobSchedules.CreateAllTriggers(Options);

            // Assert
            triggers.Should().OnlyContain(
                trigger => trigger.MisfireInstruction == expectedInstruction);
        }

        [TestCase(typeof(PremierLeagueNotificationJob))]
        [TestCase(typeof(UclDeadlineNotificationJob))]
        [TestCase(typeof(PremierLeagueClassicStandingsInformationJob))]
        [TestCase(typeof(PremierLeagueH2hStandingsInformationJob))]
        [TestCase(typeof(BenchWarmingLeagueCalculationJob))]
        public void JobType_NetworkAndDeliveryWork_DisallowsConcurrentExecution(Type jobType)
        {
            // Arrange
            var attributeType = typeof(DisallowConcurrentExecutionAttribute);

            // Act
            var attribute = Attribute.GetCustomAttribute(jobType, attributeType);

            // Assert
            attribute.Should().NotBeNull();
        }
    }
}
