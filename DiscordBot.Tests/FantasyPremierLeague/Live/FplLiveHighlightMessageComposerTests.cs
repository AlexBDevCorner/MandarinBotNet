using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Live;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Live;

[TestFixture]
public sealed class FplLiveHighlightMessageComposerTests
{
    private static readonly DateTimeOffset DetectedAtUtc =
        FplLiveTestData.CapturedAtUtc;

    [Test]
    public void Compose_AllHighlightTypes_ReturnsConciseSanitizedDigest()
    {
        // Arrange
        FplLiveHighlight[] highlights =
        [
            new LeaderChangedHighlight(
                1,
                "Alpha",
                2,
                "@everyone Beta",
                146,
                3,
                DetectedAtUtc),
            new SignificantRankChangeHighlight(3, "Mike", 5, 2, DetectedAtUtc),
            new BenchThresholdReachedHighlight(1, "Alex", 13, DetectedAtUtc),
            new CaptainSuccessHighlight(2, "Bob", "@here Salah", 24, DetectedAtUtc),
            new AutomaticSubstitutionHighlight(
                1,
                "Alex",
                "Out",
                "In",
                8,
                DetectedAtUtc)
        ];
        var composer = new FplLiveHighlightMessageComposer();

        // Act
        var message = composer.Compose(CreateDigest(highlights));

        // Assert
        message.Should().StartWith("⚡ FPL live — тур 3");
        message.Should().Contain("🥇 @\u200Beveryone Beta выходит на первое место — 146 очков, +3");
        message.Should().Contain("🔥 Mike поднялся с 5-го на 2-е место.");
        message.Should().Contain("🪑 Alex оставил 13 очков на скамейке.");
        message.Should().Contain("🧠 @\u200Bhere Salah принёс Bob 24 капитанских очка.");
        message.Should().Contain("🛟 Alex спас 8 очков автозаменой: In вместо Out.");
        message.Should().EndWith("Полная картина: /live");
        message.Should().NotContain("@everyone");
        message.Should().NotContain("@here");
    }

    [Test]
    public void Compose_MoreThanFiveHighlights_LimitsLinesAndReportsRemainder()
    {
        // Arrange
        var highlights = Enumerable.Range(1, 8)
            .Select(entryId => (FplLiveHighlight)new BenchThresholdReachedHighlight(
                entryId,
                $"Team {entryId}",
                8 + entryId,
                DetectedAtUtc))
            .ToArray();
        var composer = new FplLiveHighlightMessageComposer();

        // Act
        var message = composer.Compose(CreateDigest(highlights));

        // Assert
        message.Split('\n').Count(line => line.StartsWith("🪑", StringComparison.Ordinal))
            .Should().Be(FplLiveHighlightMessageComposer.MaximumHighlightLines);
        message.Should().Contain("...и ещё 3 изменения.");
        message.Should().EndWith("Полная картина: /live");
    }

    [Test]
    public void Compose_CaptainDisaster_FormatsCaptainAndViceCaptainScores()
    {
        // Arrange
        var composer = new FplLiveHighlightMessageComposer();
        var highlight = new CaptainDisasterHighlight(
            1,
            "Alpha",
            "Haaland",
            1,
            "Palmer",
            9,
            DetectedAtUtc);

        // Act
        var message = composer.Compose(CreateDigest([highlight]));

        // Assert
        message.Should().Contain(
            "💥 У Alpha капитан Haaland набрал 1, а вице-капитан Palmer — 9.");
    }

    private static FplLiveDigest CreateDigest(
        IReadOnlyList<FplLiveHighlight> highlights)
    {
        return new FplLiveDigest(
            123,
            "2026/27",
            3,
            10,
            100,
            1,
            "2026-27-event-3-live-digest-0001",
            highlights);
    }
}
