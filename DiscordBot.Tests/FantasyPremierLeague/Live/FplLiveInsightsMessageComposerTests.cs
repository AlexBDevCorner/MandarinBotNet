using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Live;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Live;

[TestFixture]
public sealed class FplLiveInsightsMessageComposerTests
{
    [Test]
    public void Compose_AvailableInsights_IncludesFreshnessManagersThresholdsAndAlerts()
    {
        // Arrange
        var options = new FantasyPremierLeagueOptions
        {
            LargeBenchPointsThreshold = 8,
            CaptainSuccessEffectivePointsThreshold = 20,
            CaptainDisasterPointsThreshold = 2,
            CaptainDisasterViceCaptainPointsThreshold = 8
        };
        var gameweek = new FplLiveGameweek(
            "2026/27",
            5,
            new DateTimeOffset(2026, 8, 21, 18, 45, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 21, 18, 50, 0, TimeSpan.Zero),
            [
                CreateManager("@everyone Alpha", "@here Alice", 1, 64, 2, 2),
                CreateManager("Beta", "Bob", 2, 52, 1, 0)
            ],
            [CreateManager("@everyone Alpha", "@here Alice", 1, 64, 2, 2)],
            [CreateManager("@everyone Alpha", "@here Alice", 1, 64, 2, 2)],
            [CreateManager("Beta", "Bob", 2, 52, 1, 0)],
            [new FplAutomaticSubstitutionSalvation(
                1,
                "@everyone Alpha",
                "Bench In",
                9,
                "Starter Out",
                0,
                9)]);
        var composer = new FplLiveInsightsMessageComposer(options);

        // Act
        var message = composer.Compose(FplLiveInsightsResult.Available(gameweek));

        // Assert
        message.Should().Contain("Gameweek 5 (season 2026/27)");
        message.Should().Contain("Source updated: 2026-08-21 18:45:00 UTC");
        message.Should().Contain("captured: 2026-08-21 18:50:00 UTC");
        message.Should().Contain("@\u200Beveryone Alpha");
        message.Should().Contain("@\u200Bhere Alice");
        message.Should().Contain("64 live points; 2 players remaining");
        message.Should().Contain("Bench alerts (>= 8 points):");
        message.Should().Contain("Automatic-substitution salvation:");
        message.Should().Contain("Bench In (9) replaced Starter Out (0); +9 points saved");
        message.Should().Contain("Captain disasters (captain <= 2, vice-captain >= 8):");
        message.Should().Contain("Captain successes (effective points >= 20):");
        message.Should().NotContain("@everyone");
        message.Should().NotContain("@here");
    }

    [Test]
    public void Compose_StaleInsights_IdentifiesGameweekAndSourceAgeLimit()
    {
        // Arrange
        var gameweek = new FplLiveGameweek(
            "2026/27",
            5,
            new DateTimeOffset(2026, 8, 21, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 21, 18, 30, 0, TimeSpan.Zero),
            [],
            [],
            [],
            [],
            []);
        var composer = new FplLiveInsightsMessageComposer(
            new FantasyPremierLeagueOptions
            {
                LiveDataMaxAge = TimeSpan.FromMinutes(20)
            });

        // Act
        var message = composer.Compose(FplLiveInsightsResult.Stale(gameweek));

        // Assert
        message.Should().Contain("Gameweek 5");
        message.Should().Contain("Source updated: 2026-08-21 18:00:00 UTC");
        message.Should().Contain("maximum age: 20 minutes");
        message.Should().Contain("stale");
    }

    [Test]
    public void SourceIdentifier_SameSourceUpdateIsStable_ChangedSourceUpdateCreatesNewCheckpoint()
    {
        // Arrange
        var gameweek = new FplLiveGameweek(
            "2026/27",
            5,
            new DateTimeOffset(2026, 8, 21, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 21, 18, 5, 0, TimeSpan.Zero),
            [],
            [],
            [],
            [],
            []);

        // Act
        var firstIdentifier = FplLiveInsightsSourceIdentifier.Create(gameweek);
        var repeatedIdentifier = FplLiveInsightsSourceIdentifier.Create(gameweek);
        var updatedIdentifier = FplLiveInsightsSourceIdentifier.Create(gameweek with
        {
            SourceUpdatedAtUtc = gameweek.SourceUpdatedAtUtc.AddMinutes(1)
        });

        // Assert
        repeatedIdentifier.Should().Be(firstIdentifier);
        updatedIdentifier.Should().NotBe(firstIdentifier);
    }

    private static FplLiveManagerInsights CreateManager(
        string entryName,
        string managerName,
        int rank,
        int livePoints,
        int playersRemaining,
        int benchPoints)
    {
        return new FplLiveManagerInsights(
            rank,
            entryName,
            managerName,
            rank,
            livePoints,
            playersRemaining,
            benchPoints,
            new FplLiveCaptainInsights(
                "Captain",
                rank == 1 ? 1 : 10,
                rank == 1 ? 2 : 20,
                "Vice",
                rank == 1 ? 10 : 1,
                rank == 1 ? 10 : 1),
            []);
    }
}
