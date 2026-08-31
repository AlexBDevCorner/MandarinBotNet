using AwesomeAssertions;
using DiscordBot;
using DiscordBot.FantasyPremierLeague.ChipWatch;
using DiscordBot.FantasyPremierLeague.Chips;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.ChipWatch;

[TestFixture]
public sealed class FplChipWatchNotificationEligibilityServiceTests
{
    private readonly FplChipWatchNotificationEligibilityService _sut = new();

    private static FplChipWatchOptions Options(int minScore = 60, int start = 36, int end = 18) =>
        new() { MinimumNotificationScore = minScore, NotificationWindowStartHours = start, NotificationWindowEndHours = end };

    [Test]
    public void IsWithinWindow_BeforeStart_NotEligible()
    {
        var now = new DateTimeOffset(2027, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var deadline = now.AddHours(40);
        _sut.IsWithinWindow(now, deadline, Options()).Should().BeFalse();
    }

    [Test]
    public void IsWithinWindow_At36Hours_Eligible()
    {
        var now = new DateTimeOffset(2027, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var deadline = now.AddHours(36);
        _sut.IsWithinWindow(now, deadline, Options()).Should().BeTrue();
    }

    [Test]
    public void IsWithinWindow_At18Hours_Eligible()
    {
        var now = new DateTimeOffset(2027, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var deadline = now.AddHours(18);
        _sut.IsWithinWindow(now, deadline, Options()).Should().BeTrue();
    }

    [Test]
    public void IsWithinWindow_AfterEnd_NotEligible()
    {
        var now = new DateTimeOffset(2027, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var deadline = now.AddHours(10);
        _sut.IsWithinWindow(now, deadline, Options()).Should().BeFalse();
    }

    [Test]
    public void HasMeaningfulSignals_StrongFreeHit_ReturnsTrue()
    {
        var report = CreateReportWithFreeHit(70, FplChipOpportunityLevel.Strong);
        _sut.HasMeaningfulSignals(report, Options()).Should().BeTrue();
    }

    [Test]
    public void HasMeaningfulSignals_OnlyLowUrgency_ReturnsFalse()
    {
        var report = CreateReportWithExpiry(FplChipUrgency.Low);
        _sut.HasMeaningfulSignals(report, Options()).Should().BeFalse();
    }

    [Test]
    public void HasMeaningfulSignals_HighExpiry_ReturnsTrue()
    {
        var report = CreateReportWithExpiry(FplChipUrgency.High);
        _sut.HasMeaningfulSignals(report, Options()).Should().BeTrue();
    }

    [Test]
    public void HasMeaningfulSignals_CustomThreshold_Respected()
    {
        var report = CreateReportWithFreeHit(65, FplChipOpportunityLevel.Strong);
        _sut.HasMeaningfulSignals(report, Options(minScore: 75)).Should().BeFalse();
        _sut.HasMeaningfulSignals(report, Options(minScore: 60)).Should().BeTrue();
    }

    [Test]
    public void IsNotificationWorthy_BelowThreshold_False()
    {
        var rec = new FplChipRecommendation(FplChipType.FreeHit, 65, FplChipOpportunityLevel.Strong, FplChipUrgency.None, []);
        _sut.IsNotificationWorthy(rec, Options(minScore: 75)).Should().BeFalse();
    }

    [Test]
    public void IsNotificationWorthy_StrongAndAboveThreshold_True()
    {
        var rec = new FplChipRecommendation(FplChipType.FreeHit, 75, FplChipOpportunityLevel.VeryStrong, FplChipUrgency.None, []);
        _sut.IsNotificationWorthy(rec, Options(minScore: 60)).Should().BeTrue();
    }

    private static FplChipWatchReport CreateReportWithFreeHit(int score, FplChipOpportunityLevel level)
    {
        var rec = new FplChipRecommendation(FplChipType.FreeHit, score, level, FplChipUrgency.None, []);
        var manager = new FplManagerChipWatch(1, "Team", "Player", 10, [], rec, true, true);
        return new FplChipWatchReport(12, DateTimeOffset.UtcNow.AddHours(20), 11, 38, 1, 1, 1, [manager]);
    }

    private static FplChipWatchReport CreateReportWithExpiry(FplChipUrgency urgency)
    {
        var chips = new[] { new FplChipAvailability(FplChipType.Wildcard, true, null, urgency) };
        var manager = new FplManagerChipWatch(1, "Team", "Player", 10, chips, null, true, true);
        return new FplChipWatchReport(12, DateTimeOffset.UtcNow.AddHours(20), 11, 38, 1, 1, 1, [manager]);
    }
}
