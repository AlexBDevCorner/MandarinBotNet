using AwesomeAssertions;
using DiscordBot.Jobs;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.Jobs
{
    [TestFixture]
    public sealed class JobSchedulesTests
    {
        [Test]
        public void CreatePremierLeagueNotificationTrigger_DefaultSchedule_RunsHourlyInRiga()
        {
            // Arrange
            var expectedTimeZone = JobSchedules.RigaTimeZone;

            // Act
            var trigger = JobSchedules.CreatePremierLeagueNotificationTrigger();

            // Assert
            var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
            cronTrigger.CronExpressionString.Should().Be(JobSchedules.PremierLeagueNotificationCron);
            cronTrigger.TimeZone.Should().Be(expectedTimeZone);
        }

        [Test]
        public void CreateDailyStandingsTrigger_WinterAndSummer_FiresAtConfiguredRigaLocalTime()
        {
            // Arrange
            var trigger = JobSchedules.CreatePremierLeagueClassicStandingsInformationTrigger();
            var winterReference = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);
            var summerReference = new DateTimeOffset(2027, 7, 15, 12, 0, 0, TimeSpan.Zero);

            // Act
            var winterFireTime = trigger.GetFireTimeAfter(winterReference);
            var summerFireTime = trigger.GetFireTimeAfter(summerReference);
            var winterRigaTime = TimeZoneInfo.ConvertTime(
                winterFireTime!.Value,
                JobSchedules.RigaTimeZone);
            var summerRigaTime = TimeZoneInfo.ConvertTime(
                summerFireTime!.Value,
                JobSchedules.RigaTimeZone);

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
            var trigger = JobSchedules.CreatePremierLeagueClassicStandingsInformationTrigger();
            var reference = new DateTimeOffset(2027, 3, 26, 16, 0, 0, TimeSpan.Zero);

            // Act
            var beforeTransition = trigger.GetFireTimeAfter(reference)!.Value;
            var transitionDay = trigger.GetFireTimeAfter(beforeTransition)!.Value;
            var afterTransition = trigger.GetFireTimeAfter(transitionDay)!.Value;
            var rigaFireTimes = new[] { beforeTransition, transitionDay, afterTransition }
                .Select(time => TimeZoneInfo.ConvertTime(time, JobSchedules.RigaTimeZone))
                .ToArray();

            // Assert
            rigaFireTimes.Should().OnlyContain(time => time.Hour == 17);
            (transitionDay - beforeTransition).Should().Be(TimeSpan.FromHours(23));
            (afterTransition - transitionDay).Should().Be(TimeSpan.FromHours(24));
        }

        [Test]
        public void CreateAllTriggers_DefaultSchedules_SkipMissedRuns()
        {
            // Arrange
            var expectedInstruction = MisfireInstruction.CronTrigger.DoNothing;

            // Act
            var triggers = JobSchedules.CreateAllTriggers();

            // Assert
            triggers.Should().OnlyContain(
                trigger => trigger.MisfireInstruction == expectedInstruction);
        }

        [TestCase(typeof(PremierLeagueNotificationJob))]
        [TestCase(typeof(PremierLeagueClassicStandingsInformationJob))]
        [TestCase(typeof(PremierLeagueH2hStandingsInformationJob))]
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
