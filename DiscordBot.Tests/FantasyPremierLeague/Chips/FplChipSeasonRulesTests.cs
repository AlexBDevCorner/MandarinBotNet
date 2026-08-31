using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Chips;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Chips;

[TestFixture]
public sealed class FplChipSeasonRulesTests
{
    private readonly FplChipSeasonRules _sut = new();

    [Test]
    public void CanChipNormallyBePlayed_ManagerStartedGw1_TargetGw1_WcUnavailable()
    {
        var history = Array.Empty<FplPlayedChip>();
        _sut.CanChipNormallyBePlayed(FplChipType.Wildcard, 1, 1, history).Should().BeFalse();
        _sut.CanChipNormallyBePlayed(FplChipType.FreeHit, 1, 1, history).Should().BeFalse();
    }

    [Test]
    public void CanChipNormallyBePlayed_ManagerStartedGw5_TargetGw5_WcFhUnavailable()
    {
        var history = Array.Empty<FplPlayedChip>();
        _sut.CanChipNormallyBePlayed(FplChipType.Wildcard, 5, 5, history).Should().BeFalse();
        _sut.CanChipNormallyBePlayed(FplChipType.FreeHit, 5, 5, history).Should().BeFalse();
    }

    [Test]
    public void CanChipNormallyBePlayed_ManagerStartedGw5_TargetGw6_WcFhAvailable()
    {
        var history = Array.Empty<FplPlayedChip>();
        _sut.CanChipNormallyBePlayed(FplChipType.Wildcard, 6, 5, history).Should().BeTrue();
        _sut.CanChipNormallyBePlayed(FplChipType.FreeHit, 6, 5, history).Should().BeTrue();
    }

    [Test]
    public void CanChipNormallyBePlayed_BenchBoostAndTc_AreNotAffectedByOpening()
    {
        var history = Array.Empty<FplPlayedChip>();
        _sut.CanChipNormallyBePlayed(FplChipType.BenchBoost, 1, 1, history).Should().BeTrue();
        _sut.CanChipNormallyBePlayed(FplChipType.TripleCaptain, 1, 1, history).Should().BeTrue();
        _sut.CanChipNormallyBePlayed(FplChipType.BenchBoost, 5, 5, history).Should().BeTrue();
        _sut.CanChipNormallyBePlayed(FplChipType.TripleCaptain, 5, 5, history).Should().BeTrue();
    }

    [Test]
    public void CanChipNormallyBePlayed_FreeHitConsecutive_Blocked()
    {
        var history = new[] { new FplPlayedChip(FplChipType.FreeHit, 19) };
        _sut.CanChipNormallyBePlayed(FplChipType.FreeHit, 20, 1, history).Should().BeFalse();
    }

    [Test]
    public void CanChipNormallyBePlayed_FreeHitConsecutive_AllowsGw21()
    {
        var history = new[] { new FplPlayedChip(FplChipType.FreeHit, 19) };
        _sut.CanChipNormallyBePlayed(FplChipType.FreeHit, 21, 1, history).Should().BeTrue();
    }

    [Test]
    public void GetPeriod_FirstHalf()
    {
        _sut.GetPeriod(1, 38).Should().Be(FplChipPeriod.FirstHalf);
        _sut.GetPeriod(19, 38).Should().Be(FplChipPeriod.FirstHalf);
    }

    [Test]
    public void GetPeriod_SecondHalf()
    {
        _sut.GetPeriod(20, 38).Should().Be(FplChipPeriod.SecondHalf);
        _sut.GetPeriod(38, 38).Should().Be(FplChipPeriod.SecondHalf);
    }

    [Test]
    public void GetUrgency_FirstHalf()
    {
        _sut.GetUrgency(FplChipType.FreeHit, 16, 38, true).Should().Be(FplChipUrgency.None);
        _sut.GetUrgency(FplChipType.FreeHit, 17, 38, true).Should().Be(FplChipUrgency.Low);
        _sut.GetUrgency(FplChipType.FreeHit, 18, 38, true).Should().Be(FplChipUrgency.High);
        _sut.GetUrgency(FplChipType.FreeHit, 19, 38, true).Should().Be(FplChipUrgency.Critical);
    }

    [Test]
    public void GetUrgency_SecondHalf()
    {
        _sut.GetUrgency(FplChipType.FreeHit, 36, 38, true).Should().Be(FplChipUrgency.Low);
        _sut.GetUrgency(FplChipType.FreeHit, 37, 38, true).Should().Be(FplChipUrgency.High);
        _sut.GetUrgency(FplChipType.FreeHit, 38, 38, true).Should().Be(FplChipUrgency.Critical);
        _sut.GetUrgency(FplChipType.FreeHit, 20, 38, true).Should().Be(FplChipUrgency.None);
    }

    [Test]
    public void GetUrgency_NotAvailable_ReturnsNone()
    {
        _sut.GetUrgency(FplChipType.FreeHit, 19, 38, false).Should().Be(FplChipUrgency.None);
    }
}
