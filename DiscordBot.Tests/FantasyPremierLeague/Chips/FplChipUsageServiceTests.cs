using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Chips;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Chips;

[TestFixture]
public sealed class FplChipUsageServiceTests
{
    private readonly FplChipUsageService _sut = new(new FplChipSeasonRules(), NullLogger<FplChipUsageService>.Instance);

    [Test]
    public void MapHistory_NoChips_ReturnsEmpty()
    {
        var result = _sut.MapHistory([]);
        result.Should().BeEmpty();
    }

    [TestCase("wildcard", FplChipType.Wildcard)]
    [TestCase("freehit", FplChipType.FreeHit)]
    [TestCase("bboost", FplChipType.BenchBoost)]
    [TestCase("3xc", FplChipType.TripleCaptain)]
    public void MapHistory_KnownNames_MappedCorrectly(string raw, FplChipType expected)
    {
        var chips = new[] { new EntryHistoryChip { Name = raw, EventId = 5 } };
        var result = _sut.MapHistory(chips);
        result.Should().ContainSingle().Which.Chip.Should().Be(expected);
    }

    [Test]
    public void MapHistory_UnknownName_Ignored()
    {
        var chips = new[] { new EntryHistoryChip { Name = "unknown_chip", EventId = 5 } };
        var result = _sut.MapHistory(chips);
        result.Should().BeEmpty();
    }

    [Test]
    public void GetAvailabilities_NoHistory_FirstHalfUnused()
    {
        var history = Array.Empty<FplPlayedChip>();
        var result = _sut.GetAvailabilities(10, 38, history, 1);
        result.Should().OnlyContain(a => a.IsAvailable);
        result.First(a => a.Chip == FplChipType.FreeHit).UsedEventId.Should().BeNull();
    }

    [Test]
    public void GetAvailabilities_WildcardUsedInGw5_ConsumesFirstHalf()
    {
        var history = new[] { new FplPlayedChip(FplChipType.Wildcard, 5) };
        var result = _sut.GetAvailabilities(10, 38, history, 1);
        var wc = result.First(a => a.Chip == FplChipType.Wildcard);
        wc.IsAvailable.Should().BeFalse();
        wc.UsedEventId.Should().Be(5);
        wc.UnavailabilityReason.Should().Be(FplChipUnavailabilityReason.UsedInPeriod);
    }

    [Test]
    public void GetAvailabilities_SameChipAvailableInSecondHalf()
    {
        var history = new[] { new FplPlayedChip(FplChipType.Wildcard, 5) };
        var result = _sut.GetAvailabilities(22, 38, history, 1);
        var wc = result.First(a => a.Chip == FplChipType.Wildcard);
        wc.IsAvailable.Should().BeTrue();
    }

    [Test]
    public void GetAvailabilities_FirstUnusedExpiresAfterGw19()
    {
        var history = Array.Empty<FplPlayedChip>();
        var result = _sut.GetAvailabilities(19, 38, history, 1);
        result.First(a => a.Chip == FplChipType.FreeHit).Urgency.Should().Be(FplChipUrgency.Critical);
    }

    [Test]
    public void GetAvailabilities_WcUnavailableInOpeningGw()
    {
        var history = Array.Empty<FplPlayedChip>();
        var result = _sut.GetAvailabilities(5, 38, history, 5);
        result.First(a => a.Chip == FplChipType.Wildcard).IsAvailable.Should().BeFalse();
        result.First(a => a.Chip == FplChipType.Wildcard).UnavailabilityReason.Should().Be(FplChipUnavailabilityReason.OpeningGameweek);
    }

    [Test]
    public void GetAvailabilities_FreeHitUsedGw19_PreventsGw20()
    {
        var history = new[] { new FplPlayedChip(FplChipType.FreeHit, 19) };
        var result = _sut.GetAvailabilities(20, 38, history, 1);
        var fh = result.First(a => a.Chip == FplChipType.FreeHit);
        fh.IsAvailable.Should().BeFalse();
        fh.UnavailabilityReason.Should().Be(FplChipUnavailabilityReason.ConsecutiveFreeHit);
        fh.UsedEventId.Should().Be(19);
        fh.BlockingChip.Should().Be(FplChipType.FreeHit);
    }

    [Test]
    public void GetAvailabilities_FreeHitUsedGw19_AvailableGw21()
    {
        var history = new[] { new FplPlayedChip(FplChipType.FreeHit, 19) };
        var result = _sut.GetAvailabilities(21, 38, history, 1);
        result.First(a => a.Chip == FplChipType.FreeHit).IsAvailable.Should().BeTrue();
    }

    [Test]
    public void GetAvailabilities_UrgencyGw17Low()
    {
        var history = Array.Empty<FplPlayedChip>();
        var result = _sut.GetAvailabilities(17, 38, history, 1);
        result.First(a => a.Chip == FplChipType.FreeHit).Urgency.Should().Be(FplChipUrgency.Low);
    }

    [Test]
    public void GetAvailabilities_UrgencyGw18High()
    {
        var history = Array.Empty<FplPlayedChip>();
        var result = _sut.GetAvailabilities(18, 38, history, 1);
        result.First(a => a.Chip == FplChipType.FreeHit).Urgency.Should().Be(FplChipUrgency.High);
    }

    [Test]
    public void GetAvailabilities_UrgencyGw19Critical()
    {
        var history = Array.Empty<FplPlayedChip>();
        var result = _sut.GetAvailabilities(19, 38, history, 1);
        result.First(a => a.Chip == FplChipType.FreeHit).Urgency.Should().Be(FplChipUrgency.Critical);
    }

    [Test]
    public void GetAvailabilities_SecondHalfUrgencyDerivedFromFinal()
    {
        var history = Array.Empty<FplPlayedChip>();
        _sut.GetAvailabilities(36, 38, history, 1).First(a => a.Chip == FplChipType.FreeHit).Urgency.Should().Be(FplChipUrgency.Low);
        _sut.GetAvailabilities(37, 38, history, 1).First(a => a.Chip == FplChipType.FreeHit).Urgency.Should().Be(FplChipUrgency.High);
        _sut.GetAvailabilities(38, 38, history, 1).First(a => a.Chip == FplChipType.FreeHit).Urgency.Should().Be(FplChipUrgency.Critical);
    }

    [Test]
    public void GetAvailabilities_AnotherChipActive_BlocksOtherChips()
    {
        var history = new[] { new FplPlayedChip(FplChipType.TripleCaptain, 18) };
        var result = _sut.GetAvailabilities(18, 38, history, 1);
        var fh = result.First(a => a.Chip == FplChipType.FreeHit);
        fh.IsAvailable.Should().BeFalse();
        fh.UnavailabilityReason.Should().Be(FplChipUnavailabilityReason.AnotherChipActive);
        fh.BlockingChip.Should().Be(FplChipType.TripleCaptain);
    }

    [Test]
    public void GetAvailabilities_UsedEventExposed()
    {
        var history = new[] { new FplPlayedChip(FplChipType.BenchBoost, 4) };
        var result = _sut.GetAvailabilities(10, 38, history, 1);
        result.First(a => a.Chip == FplChipType.BenchBoost).UsedEventId.Should().Be(4);
    }
}
