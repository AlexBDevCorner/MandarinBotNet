using DiscordBot.FantasyPremierLeague.Chips;
using DiscordBot.Responses;

namespace DiscordBot.FantasyPremierLeague.ChipWatch;

public sealed class FplFreeHitOpportunityService
{
    public FplChipRecommendation? Evaluate(
        FplChipAvailability freeHitAvailability,
        IReadOnlyList<EntryEventPick> picks,
        IReadOnlyDictionary<int, PremierLeagueElement> elementsById,
        IReadOnlyDictionary<int, IReadOnlyList<PremierLeagueFixture>> fixturesByTeam)
    {
        ArgumentNullException.ThrowIfNull(freeHitAvailability);
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(elementsById);
        ArgumentNullException.ThrowIfNull(fixturesByTeam);

        if (freeHitAvailability.Chip != FplChipType.FreeHit)
        {
            throw new ArgumentException("Availability must be for FreeHit.", nameof(freeHitAvailability));
        }

        if (!freeHitAvailability.IsAvailable)
        {
            return null;
        }

        if (picks.Count == 0)
        {
            return null;
        }

        int blankCount = 0;
        int unavailableCount = 0;
        int doubtfulCount = 0;
        int difficultCount = 0;

        foreach (var pick in picks)
        {
            if (!elementsById.TryGetValue(pick.Element, out var element))
            {
                continue;
            }

            fixturesByTeam.TryGetValue(element.TeamId, out var fixtures);
            fixtures ??= Array.Empty<PremierLeagueFixture>();

            var isBlank = fixtures.Count == 0;
            if (isBlank)
            {
                blankCount++;
                continue;
            }

            var availability = ClassifyAvailability(element);
            if (availability == PlayerAvailability.Unavailable)
            {
                unavailableCount++;
            }
            else if (availability == PlayerAvailability.Doubtful)
            {
                doubtfulCount++;
            }

            // Fixture difficulty is an independent signal; a doubtful/unavailable player can also have a difficult fixture.
            if (IsDifficult(fixtures, element.TeamId))
            {
                difficultCount++;
            }
        }

        // Calculate scores
        var blankScore = CalculateBlankScore(blankCount);
        var unavailableScore = Math.Min(unavailableCount * 8, 24);
        var doubtfulScore = Math.Min(doubtfulCount * 4, 12);
        var difficultScore = Math.Min(difficultCount * 3, 15);

        var rawScore = blankScore + unavailableScore + doubtfulScore + difficultScore;
        var clamped = Math.Clamp(rawScore, 0, 100);

        // Noise protection: if blank <3, cap at 59
        if (blankCount < 3 && clamped > 59)
        {
            clamped = 59;
        }

        var level = GetLevel(clamped);

        var reasons = new List<FplChipRecommendationReason>();
        if (blankCount > 0)
        {
            reasons.Add(new FplChipRecommendationReason(FplChipRecommendationReasonKind.BlankPlayers, blankCount));
        }

        if (unavailableCount > 0)
        {
            reasons.Add(new FplChipRecommendationReason(FplChipRecommendationReasonKind.UnavailablePlayers, unavailableCount));
        }

        if (doubtfulCount > 0)
        {
            reasons.Add(new FplChipRecommendationReason(FplChipRecommendationReasonKind.DoubtfulPlayers, doubtfulCount));
        }

        if (difficultCount > 0)
        {
            reasons.Add(new FplChipRecommendationReason(FplChipRecommendationReasonKind.DifficultFixtures, difficultCount));
        }

        return new FplChipRecommendation(
            FplChipType.FreeHit,
            clamped,
            level,
            freeHitAvailability.Urgency,
            reasons);
    }

    private static int CalculateBlankScore(int blankCount)
    {
        if (blankCount <= 0)
        {
            return 0;
        }

        var score = 0;
        var firstThree = Math.Min(blankCount, 3);
        score += firstThree * 12;
        if (blankCount > 3)
        {
            score += (blankCount - 3) * 8;
        }

        return Math.Min(score, 60);
    }

    private static FplChipOpportunityLevel GetLevel(int score)
    {
        if (score >= 75)
        {
            return FplChipOpportunityLevel.VeryStrong;
        }

        if (score >= 60)
        {
            return FplChipOpportunityLevel.Strong;
        }

        if (score >= 40)
        {
            return FplChipOpportunityLevel.Consider;
        }

        return FplChipOpportunityLevel.None;
    }

    private static bool IsDifficult(IReadOnlyList<PremierLeagueFixture> fixtures, int teamId)
    {
        if (fixtures.Count == 0)
        {
            return false;
        }

        foreach (var fixture in fixtures)
        {
            int difficulty;
            if (fixture.HomeTeamId == teamId)
            {
                difficulty = fixture.HomeTeamDifficulty;
            }
            else if (fixture.AwayTeamId == teamId)
            {
                difficulty = fixture.AwayTeamDifficulty;
            }
            else
            {
                // Should not happen; if team not involved, treat as not difficult
                return false;
            }

            if (difficulty < 4)
            {
                return false;
            }
        }

        return true;
    }

    private enum PlayerAvailability
    {
        Available,
        Unavailable,
        Doubtful
    }

    private static PlayerAvailability ClassifyAvailability(PremierLeagueElement element)
    {
        var status = element.Status?.Trim().ToLowerInvariant() ?? string.Empty;

        if (status is "u" or "i" or "s" or "n")
        {
            return PlayerAvailability.Unavailable;
        }

        if (element.ChanceOfPlayingNextRound == 0)
        {
            return PlayerAvailability.Unavailable;
        }

        if (status == "d")
        {
            return PlayerAvailability.Doubtful;
        }

        if (element.ChanceOfPlayingNextRound is > 0 and <= 50)
        {
            return PlayerAvailability.Doubtful;
        }

        return PlayerAvailability.Available;
    }
}
