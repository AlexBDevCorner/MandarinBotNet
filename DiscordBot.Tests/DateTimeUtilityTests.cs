using AwesomeAssertions;
using NUnit.Framework;

namespace DiscordBot.Tests;

[TestFixture]
public sealed class DateTimeUtilityTests
{
    [TestCase(1, 1, 1, "1 день, 1 час, 1 минута")]
    [TestCase(2, 2, 2, "2 дня, 2 часа, 2 минуты")]
    [TestCase(5, 5, 5, "5 дней, 5 часов, 5 минут")]
    [TestCase(11, 11, 11, "11 дней, 11 часов, 11 минут")]
    [TestCase(21, 21, 21, "21 день, 21 час, 21 минута")]
    [TestCase(22, 22, 22, "22 дня, 22 часа, 22 минуты")]
    public void GenerateRemainingDaysMessageInRussian_Duration_UsesRussianPluralForms(
        int days,
        int hours,
        int minutes,
        string expected)
    {
        // Arrange
        var duration = TimeSpan.FromDays(days) +
            TimeSpan.FromHours(hours) +
            TimeSpan.FromMinutes(minutes);

        // Act
        var result = DateTimeUtility.GenerateRemainingDaysMessageInRussian(duration);

        // Assert
        result.Should().Be(expected);
    }
}
