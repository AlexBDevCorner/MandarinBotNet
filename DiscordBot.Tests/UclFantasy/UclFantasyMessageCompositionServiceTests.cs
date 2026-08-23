using AwesomeAssertions;
using DiscordBot.PremierLeague;
using DiscordBot.UclFantasy;
using NUnit.Framework;

namespace DiscordBot.Tests.UclFantasy;

[TestFixture]
public sealed class UclFantasyMessageCompositionServiceTests
{
    [Test]
    public void ComposeDeadlineReminder_UpcomingDeadline_ReturnsRussianMessageWithEmoji()
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
        message.Should().Be(
            "⚽ Дедлайн игрового дня ЛЧ: 08 сентября 2026, 19:45 " +
            "(Рига, Латвия). Осталось: 24 ч 5 мин. ⏳");
    }
}
