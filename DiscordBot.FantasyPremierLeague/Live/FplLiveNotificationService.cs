using System.Globalization;
using System.Text;

namespace DiscordBot.FantasyPremierLeague.Live;

public sealed class FplLiveNotificationService(
    FantasyPremierLeagueOptions options,
    FplLiveHighlightDetectionService highlightDetectionService,
    IFplLiveNotificationStateStore stateStore,
    TimeProvider timeProvider)
{
    public FplLiveNotificationEvaluation Observe(
        FplLiveGameweek current,
        IReadOnlyList<NotificationTargetOptions> targets)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(targets);

        var targetKeys = ValidateAndIndexTargets(targets);
        var state = stateStore.Get(
            options.ClassicLeagueId,
            current.Season,
            current.EventId);

        if (state is null)
        {
            stateStore.Save(new FplLiveNotificationState(
                options.ClassicLeagueId,
                current.Season,
                current.EventId,
                current,
                targetKeys.Keys
                    .Select(key => CreateEmptyTargetState(key))
                    .ToArray()));

            return new FplLiveNotificationEvaluation(
                FplLiveNotificationOutcome.BaselineEstablished,
                0,
                []);
        }

        var detected = highlightDetectionService.Detect(
            state.LastObservedSnapshot,
            current);
        var existingByTarget = state.Targets.ToDictionary(
            target => new FplLiveTargetKey(target.GuildId, target.ChannelId));
        var nextByTarget = new Dictionary<
            FplLiveTargetKey,
            FplLiveTargetNotificationState>(existingByTarget);

        foreach (var targetKey in targetKeys.Keys)
        {
            var existing = existingByTarget.GetValueOrDefault(targetKey)
                ?? CreateEmptyTargetState(targetKey);
            var pending = existing.PendingHighlights.ToDictionary(
                highlight => highlight.Key,
                StringComparer.Ordinal);

            foreach (var highlight in detected)
            {
                MergeHighlight(pending, highlight);
            }

            pending = ReconcilePendingHighlights(pending.Values, current);

            nextByTarget[targetKey] = existing with
            {
                PendingHighlights = OrderHighlights(pending.Values)
            };
        }

        var nextTargets = nextByTarget.Values
            .OrderBy(target => target.GuildId)
            .ThenBy(target => target.ChannelId)
            .ToArray();
        var activeTargets = targetKeys.Keys
            .Select(targetKey => nextByTarget[targetKey])
            .ToArray();

        var nextState = state with
        {
            LastObservedSnapshot = current,
            Targets = nextTargets
        };
        stateStore.Save(nextState);

        var now = timeProvider.GetUtcNow();
        var cooldown = TimeSpan.FromMinutes(options.LiveNotificationCooldownMinutes);
        var digests = activeTargets
            .Where(target => target.PendingHighlights.Count > 0)
            .Where(target =>
                target.LastPublishedAtUtc is null ||
                now >= target.LastPublishedAtUtc.Value + cooldown)
            .Select(target => CreateDigest(nextState, target))
            .ToArray();
        var hasPending = activeTargets.Any(target => target.PendingHighlights.Count > 0);

        return new FplLiveNotificationEvaluation(
            digests.Length > 0
                ? FplLiveNotificationOutcome.DigestReady
                : hasPending
                    ? FplLiveNotificationOutcome.CooldownActive
                    : FplLiveNotificationOutcome.NothingInteresting,
            detected.Count,
            digests);
    }

    public void MarkPublished(FplLiveDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);

        var state = stateStore.Get(
            digest.ClassicLeagueId,
            digest.Season,
            digest.EventId)
            ?? throw new InvalidOperationException(
                "The FPL live notification state no longer exists.");
        var targetIndex = state.Targets
            .Select((target, index) => (target, index))
            .SingleOrDefault(item =>
                item.target.GuildId == digest.GuildId &&
                item.target.ChannelId == digest.ChannelId);
        if (targetIndex.target is null)
        {
            throw new InvalidOperationException(
                "The FPL live notification target no longer exists.");
        }

        if (targetIndex.target.NextDigestSequence != digest.Sequence)
        {
            throw new InvalidOperationException(
                "The FPL live digest sequence no longer matches the pending state.");
        }

        var publishedKeys = digest.Highlights
            .Select(highlight => highlight.Key)
            .ToHashSet(StringComparer.Ordinal);
        var targets = state.Targets.ToArray();
        targets[targetIndex.index] = targetIndex.target with
        {
            LastPublishedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
            NextDigestSequence = checked(targetIndex.target.NextDigestSequence + 1),
            PendingHighlights = targetIndex.target.PendingHighlights
                .Where(highlight => !publishedKeys.Contains(highlight.Key))
                .ToArray()
        };

        stateStore.Save(state with { Targets = targets });
    }

    private Dictionary<string, FplLiveHighlight> ReconcilePendingHighlights(
        IEnumerable<FplLiveHighlight> pendingHighlights,
        FplLiveGameweek current)
    {
        var byKey = new Dictionary<string, FplLiveHighlight>(StringComparer.Ordinal);

        foreach (var highlight in pendingHighlights)
        {
            var reconciled = ReconcileHighlight(highlight, current);
            if (reconciled is not null)
            {
                byKey[reconciled.Key] = reconciled;
            }
        }

        return byKey;
    }

    private FplLiveHighlight? ReconcileHighlight(
        FplLiveHighlight highlight,
        FplLiveGameweek current)
    {
        return highlight switch
        {
            LeaderChangedHighlight leader => ReconcileLeader(leader, current),
            SignificantRankChangeHighlight rank => ReconcileRank(rank, current),
            BenchThresholdReachedHighlight bench => ReconcileBench(bench, current),
            CaptainSuccessHighlight captain => ReconcileCaptainSuccess(captain, current),
            CaptainDisasterHighlight disaster => ReconcileCaptainDisaster(disaster, current),
            AutomaticSubstitutionHighlight substitution =>
                ReconcileAutomaticSubstitution(substitution, current),
            _ => throw new ArgumentOutOfRangeException(nameof(highlight))
        };
    }

    private static LeaderChangedHighlight? ReconcileLeader(
        LeaderChangedHighlight highlight,
        FplLiveGameweek current)
    {
        var currentLeaders = FplLiveLeadership.GetLeaders(current);
        if (currentLeaders.Count != 1)
        {
            return null;
        }

        var currentLeader = currentLeaders[0];
        if (currentLeader.EntryId == highlight.PreviousLeaderEntryId)
        {
            return null;
        }

        return highlight with
        {
            NewLeaderEntryId = currentLeader.EntryId,
            NewLeaderName = currentLeader.EntryName,
            NewLeaderPoints = currentLeader.LiveTotalPoints,
            Gap = FplLiveLeadership.GetGapToNextScoreGroup(current, currentLeader)
        };
    }

    private FplLiveHighlight? ReconcileRank(
        SignificantRankChangeHighlight highlight,
        FplLiveGameweek current)
    {
        var currentManager = current.Managers.SingleOrDefault(
            manager => manager.EntryId == highlight.EntryId);
        if (currentManager is null ||
            Math.Abs(highlight.PreviousRank - currentManager.LiveRank) <
                options.SignificantLiveRankChange)
        {
            return null;
        }

        return highlight with
        {
            EntryName = currentManager.EntryName,
            CurrentRank = currentManager.LiveRank
        };
    }

    private FplLiveHighlight? ReconcileBench(
        BenchThresholdReachedHighlight highlight,
        FplLiveGameweek current)
    {
        var currentManager = current.Managers.SingleOrDefault(
            manager => manager.EntryId == highlight.EntryId);
        if (currentManager is null ||
            currentManager.BenchPoints < options.LargeBenchPointsThreshold)
        {
            return null;
        }

        return highlight with
        {
            EntryName = currentManager.EntryName,
            BenchPoints = currentManager.BenchPoints
        };
    }

    private FplLiveHighlight? ReconcileCaptainSuccess(
        CaptainSuccessHighlight highlight,
        FplLiveGameweek current)
    {
        var currentManager = current.Managers.SingleOrDefault(
            manager => manager.EntryId == highlight.EntryId);
        if (currentManager is null ||
            currentManager.Captain.CaptainEffectivePoints <
                options.CaptainSuccessEffectivePointsThreshold)
        {
            return null;
        }

        return highlight with
        {
            EntryName = currentManager.EntryName,
            CaptainName = currentManager.Captain.CaptainName,
            EffectivePoints = currentManager.Captain.CaptainEffectivePoints
        };
    }

    private static FplLiveHighlight? ReconcileCaptainDisaster(
        CaptainDisasterHighlight highlight,
        FplLiveGameweek current)
    {
        var currentManager = current.CaptainDisasters.SingleOrDefault(
            manager => manager.EntryId == highlight.EntryId);
        if (currentManager is null)
        {
            return null;
        }

        return highlight with
        {
            EntryName = currentManager.EntryName,
            CaptainName = currentManager.Captain.CaptainName,
            CaptainPoints = currentManager.Captain.CaptainPoints,
            ViceCaptainName = currentManager.Captain.ViceCaptainName,
            ViceCaptainPoints = currentManager.Captain.ViceCaptainPoints
        };
    }

    private FplLiveHighlight? ReconcileAutomaticSubstitution(
        AutomaticSubstitutionHighlight highlight,
        FplLiveGameweek current)
    {
        var currentSubstitution = current.AutomaticSubstitutionSalvations
            .SingleOrDefault(substitution =>
                substitution.EntryId == highlight.EntryId &&
                string.Equals(
                    substitution.PlayerOutName,
                    highlight.PlayerOutName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    substitution.PlayerInName,
                    highlight.PlayerInName,
                    StringComparison.Ordinal));
        if (currentSubstitution is null ||
            currentSubstitution.SavedPoints <
                options.AutomaticSubstitutionHighlightPoints)
        {
            return null;
        }

        return highlight with
        {
            EntryName = currentSubstitution.EntryName,
            SavedPoints = currentSubstitution.SavedPoints
        };
    }

    private static void MergeHighlight(
        IDictionary<string, FplLiveHighlight> pending,
        FplLiveHighlight incoming)
    {
        var key = incoming.Key;
        if (!pending.TryGetValue(key, out var existing))
        {
            pending.Add(key, incoming);
            return;
        }

        switch (existing, incoming)
        {
            case (LeaderChangedHighlight oldLeader, LeaderChangedHighlight newLeader):
                if (oldLeader.PreviousLeaderEntryId == newLeader.NewLeaderEntryId)
                {
                    pending.Remove(key);
                    return;
                }

                pending[key] = newLeader with
                {
                    PreviousLeaderEntryId = oldLeader.PreviousLeaderEntryId,
                    PreviousLeaderName = oldLeader.PreviousLeaderName,
                    DetectedAtUtc = oldLeader.DetectedAtUtc
                };
                return;
            case (SignificantRankChangeHighlight oldRank,
                SignificantRankChangeHighlight newRank):
                if (oldRank.PreviousRank == newRank.CurrentRank)
                {
                    pending.Remove(key);
                    return;
                }

                pending[key] = newRank with
                {
                    PreviousRank = oldRank.PreviousRank,
                    DetectedAtUtc = oldRank.DetectedAtUtc
                };
                return;
            default:
                pending[key] = PreserveDetectionTime(incoming, existing.DetectedAtUtc);
                return;
        }
    }

    private static FplLiveHighlight PreserveDetectionTime(
        FplLiveHighlight highlight,
        DateTimeOffset detectedAtUtc)
    {
        return highlight switch
        {
            BenchThresholdReachedHighlight bench =>
                bench with { DetectedAtUtc = detectedAtUtc },
            CaptainSuccessHighlight captain =>
                captain with { DetectedAtUtc = detectedAtUtc },
            CaptainDisasterHighlight disaster =>
                disaster with { DetectedAtUtc = detectedAtUtc },
            AutomaticSubstitutionHighlight substitution =>
                substitution with { DetectedAtUtc = detectedAtUtc },
            _ => highlight
        };
    }

    private static FplLiveDigest CreateDigest(
        FplLiveNotificationState state,
        FplLiveTargetNotificationState target)
    {
        return new FplLiveDigest(
            state.ClassicLeagueId,
            state.Season,
            state.EventId,
            target.GuildId,
            target.ChannelId,
            target.NextDigestSequence,
            CreateSourceIdentifier(
                state.Season,
                state.EventId,
                target.NextDigestSequence),
            target.PendingHighlights);
    }

    private static string CreateSourceIdentifier(
        string season,
        int eventId,
        long sequence)
    {
        var normalizedSeason = new StringBuilder(season.Length);
        var previousWasSeparator = false;
        foreach (var character in season)
        {
            if (char.IsLetterOrDigit(character))
            {
                normalizedSeason.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && normalizedSeason.Length > 0)
            {
                normalizedSeason.Append('-');
                previousWasSeparator = true;
            }
        }

        return $"{normalizedSeason.ToString().TrimEnd('-')}-event-" +
            $"{eventId.ToString(CultureInfo.InvariantCulture)}-live-digest-" +
            sequence.ToString("D4", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<FplLiveHighlight> OrderHighlights(
        IEnumerable<FplLiveHighlight> highlights)
    {
        return highlights
            .OrderBy(GetPriority)
            .ThenBy(highlight => highlight.DetectedAtUtc)
            .ThenBy(highlight => highlight.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetPriority(FplLiveHighlight highlight)
    {
        return highlight switch
        {
            LeaderChangedHighlight => 0,
            SignificantRankChangeHighlight => 1,
            BenchThresholdReachedHighlight => 2,
            CaptainSuccessHighlight => 3,
            CaptainDisasterHighlight => 4,
            AutomaticSubstitutionHighlight => 5,
            _ => 6
        };
    }

    private static Dictionary<FplLiveTargetKey, NotificationTargetOptions>
        ValidateAndIndexTargets(IReadOnlyList<NotificationTargetOptions> targets)
    {
        var targetKeys = new Dictionary<
            FplLiveTargetKey,
            NotificationTargetOptions>();
        foreach (var target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (target.GuildId == 0 || target.ChannelId == 0)
            {
                throw new ArgumentException(
                    "FPL live notification targets must contain Discord IDs.",
                    nameof(targets));
            }

            if (!targetKeys.TryAdd(
                    new FplLiveTargetKey(target.GuildId, target.ChannelId),
                    target))
            {
                throw new ArgumentException(
                    "FPL live notification targets must be unique.",
                    nameof(targets));
            }
        }

        return targetKeys;
    }

    private static FplLiveTargetNotificationState CreateEmptyTargetState(
        FplLiveTargetKey key)
    {
        return new FplLiveTargetNotificationState(
            key.GuildId,
            key.ChannelId,
            null,
            1,
            []);
    }

    private sealed record FplLiveTargetKey(ulong GuildId, ulong ChannelId);
}
