using AwesomeAssertions;
using DiscordBot.PremierLeague;
using DiscordBot.UclFantasy;
using NUnit.Framework;

namespace DiscordBot.Tests.UclFantasy;

[TestFixture]
public sealed class UclFantasyMessageCompositionServiceTests
{
    [Test]
    public void ComposeDeadlineReminder_KnownDeadline_ContainsAbsoluteAndRelativeDiscordTimestamps()
    {
        // Arrange
        var deadline = new DateTimeOffset(
            2026,
            9,
            8,
            16,
            45,
            0,
            TimeSpan.Zero);
        var service = new UclFantasyMessageCompositionService(
            new ConfiguredTimeZone(
                new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));

        // Act
        var message = service.ComposeDeadlineReminder(
            deadline,
            deadline.AddHours(-24).AddMinutes(-5));

        // Assert
        message.Should().Contain("<t:1788885900:F>");
        message.Should().Contain("<t:1788885900:R>");
        message.Should().Contain("Дедлайн игрового дня ЛЧ");
        message.Should().NotContain("Рига");
        message.Should().NotContain("19:45");
        message.Should().NotContain("24 ч 5 мин");
    }

    [Test]
    public void ComposeDeadlineReminder_WinterDeadline_UsesUtcInstantDespiteDst()
    {
        // Arrange
        var deadline = new DateTimeOffset(
            2027,
            2,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);
        var service = new UclFantasyMessageCompositionService(
            new ConfiguredTimeZone(
                new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));

        // Act
        var message = service.ComposeDeadlineReminder(
            deadline,
            deadline.AddDays(-1));

        // Assert
        message.Should().Contain("<t:1801569600:F>");
        message.Should().Contain("<t:1801569600:R>");
        message.Should().NotContain("Рига");
        message.Should().NotContain("14:00");
    }

    [Test]
    public void ComposeDeadlineReminder_SameInstantDifferentOffsets_ProduceSameMarkup()
    {
        // Arrange
        var utcDeadline = new DateTimeOffset(
            2026,
            9,
            8,
            16,
            45,
            0,
            TimeSpan.Zero);
        var rigaLocalSameInstant = new DateTimeOffset(
            2026,
            9,
            8,
            19,
            45,
            0,
            TimeSpan.FromHours(3));
        var service = new UclFantasyMessageCompositionService(
            new ConfiguredTimeZone(
                new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));

        // Act
        var fromUtc = service.ComposeDeadlineReminder(
            utcDeadline,
            utcDeadline.AddHours(-1));
        var fromLocal = service.ComposeDeadlineReminder(
            rigaLocalSameInstant,
            utcDeadline.AddHours(-1));

        // Assert
        fromUtc.Should().Be(fromLocal);
        fromUtc.Should().Contain("<t:1788885900:F>");
        fromUtc.Should().Contain("<t:1788885900:R>");
    }

    [Test]
    public void ComposeDeadlineReminder_DifferentUtcNow_DoesNotChangeMessage()
    {
        // Arrange
        var deadline = new DateTimeOffset(
            2026,
            9,
            8,
            16,
            45,
            0,
            TimeSpan.Zero);
        var service = new UclFantasyMessageCompositionService(
            new ConfiguredTimeZone(
                new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));

        // Act
        var dayBefore = service.ComposeDeadlineReminder(
            deadline,
            deadline.AddDays(-1));
        var hourBefore = service.ComposeDeadlineReminder(
            deadline,
            deadline.AddHours(-1));

        // Assert
        dayBefore.Should().Be(hourBefore);
        dayBefore.Should().NotContain(" ч ");
        dayBefore.Should().NotContain(" мин.");
    }
}
