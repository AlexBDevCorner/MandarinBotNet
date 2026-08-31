using System.Globalization;
using System.Text;
using DiscordBot;
using DiscordBot.FantasyPremierLeague.Chips;
using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.ChipWatch;

public sealed class FplChipWatchMessageCompositionService
{
    private static readonly Dictionary<FplChipType, (string Emoji, string Name)> ChipDisplay = new()
    {
        [FplChipType.Wildcard] = ("🃏", "Wildcard"),
        [FplChipType.FreeHit] = ("🎯", "Free Hit"),
        [FplChipType.BenchBoost] = ("🪑", "Bench Boost"),
        [FplChipType.TripleCaptain] = ("👑", "Triple Captain")
    };

    public string ComposeLeagueOverview(FplChipWatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.Append("🧠 **Chip Watch — GW");
        builder.Append(report.TargetEventId.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("**");
        builder.AppendLine();

        if (report.SourceSquadEventId is not null)
        {
            builder.Append("_Составы анализируются по последним публично доступным данным из GW");
            builder.Append(report.SourceSquadEventId.Value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("._");
        }
        else
        {
            builder.AppendLine("_Нет предыдущего публично доступного состава для анализа._");
        }

        builder.AppendLine();

        // Free Hit section - only actionable (Level != None)
        var freeHitCandidates = report.Managers
            .Where(m => m.FreeHitRecommendation is not null &&
                        m.FreeHitRecommendation.Level != FplChipOpportunityLevel.None)
            .OrderByDescending(m => m.FreeHitRecommendation!.OpportunityScore)
            .ThenBy(m => m.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.EntryId)
            .ToList();

        builder.AppendLine("🎯 **Free Hit**");
        builder.AppendLine();

        if (freeHitCandidates.Count == 0)
        {
            builder.Append("🎯 Free Hit — сильных возможностей на GW");
            builder.Append(report.TargetEventId.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(" не найдено.");
        }
        else
        {
            foreach (var manager in freeHitCandidates)
            {
                var rec = manager.FreeHitRecommendation!;
                var levelWording = GetLevelWording(rec.Level);
                var icon = rec.Level switch
                {
                    FplChipOpportunityLevel.VeryStrong => "🔥",
                    FplChipOpportunityLevel.Strong => "👍",
                    FplChipOpportunityLevel.Consider => "🤔",
                    _ => "•"
                };
                builder.Append(icon);
                builder.Append(' ');
                builder.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
                builder.Append(" — ");
                builder.Append(rec.OpportunityScore.ToString(CultureInfo.InvariantCulture));
                builder.Append("/100");
                if (!string.IsNullOrEmpty(levelWording))
                {
                    builder.Append(", ");
                    builder.Append(levelWording);
                }

                builder.AppendLine();
                foreach (var reason in rec.Reasons)
                {
                    builder.Append("• ");
                    builder.Append(FormatReason(reason));
                    builder.AppendLine();
                }

                builder.AppendLine();
            }
        }

        // Expiry section
        var expiryManagers = report.Managers
            .Select(m => new
            {
                Manager = m,
                UrgentChips = m.Chips.Where(c => c.IsAvailable && (c.Urgency == FplChipUrgency.High || c.Urgency == FplChipUrgency.Critical)).ToList(),
                MaxUrgency = m.Chips.Where(c => c.IsAvailable).Select(c => c.Urgency).DefaultIfEmpty(FplChipUrgency.None).Max()
            })
            .Where(x => x.UrgentChips.Count > 0)
            .OrderByDescending(x => x.MaxUrgency)
            .ThenBy(x => x.Manager.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Manager.EntryId)
            .ToList();

        if (expiryManagers.Count > 0)
        {
            builder.AppendLine("⚠️ **Скоро сгорят фишки**");
            builder.AppendLine();
            if (report.TargetEventId == FplChipSeasonRules.FirstHalfLastEvent)
            {
                builder.AppendLine("🚨 Эти фишки сгорят после дедлайна GW19.");
                builder.AppendLine();
            }
            else if (IsSecondHalfCritical(report.TargetEventId, report.Managers))
            {
                // For second half final GW critical
                builder.AppendLine("🚨 Эти фишки сгорят после дедлайна GW" + report.TargetEventId.ToString(CultureInfo.InvariantCulture) + ".");
                builder.AppendLine();
            }

            foreach (var item in expiryManagers)
            {
                builder.Append(DiscordTextSafety.SanitizeExternalName(item.Manager.EntryName));
                builder.AppendLine(":");
                foreach (var chip in item.UrgentChips.OrderBy(c => c.Chip))
                {
                    var display = ChipDisplay[chip.Chip];
                    builder.Append(display.Emoji);
                    builder.Append(' ');
                    builder.Append(display.Name);
                    builder.AppendLine();
                }

                builder.AppendLine();
            }
        }

        // Coverage
        if (report.ManagersWithSquadData < report.ManagersTotal || report.ManagersWithChipHistory < report.ManagersTotal)
        {
            builder.Append("Данные по составам: ");
            builder.Append(report.ManagersWithSquadData.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(report.ManagersTotal.ToString(CultureInfo.InvariantCulture));
            builder.Append(" менеджеров.");
            if (report.ManagersWithChipHistory < report.ManagersTotal)
            {
                builder.Append(" История фишек: ");
                builder.Append(report.ManagersWithChipHistory.ToString(CultureInfo.InvariantCulture));
                builder.Append('/');
                builder.Append(report.ManagersTotal.ToString(CultureInfo.InvariantCulture));
                builder.Append('.');
            }

            builder.AppendLine();
        }

        builder.AppendLine();
        builder.AppendLine("_Состав мог измениться после дедлайна GW" + (report.SourceSquadEventId?.ToString(CultureInfo.InvariantCulture) ?? "?") + "._");
        builder.Append("Трансферы на GW");
        builder.Append(report.TargetEventId.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine(" до дедлайна публично не видны.");

        return builder.ToString().TrimEnd();
    }

    public string ComposeManagerDetails(FplManagerChipWatch manager, int targetEventId, DateTimeOffset deadlineUtc, int? sourceEventId, int finalEventId)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var builder = new StringBuilder();
        builder.Append("🧠 **Chip Watch — ");
        builder.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
        builder.Append(" — GW");
        builder.Append(targetEventId.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("**");
        builder.AppendLine();

        if (sourceEventId is not null)
        {
            builder.Append("Источник состава: GW");
            builder.Append(sourceEventId.Value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(" — последний публично доступный состав.");
        }
        else
        {
            builder.AppendLine("Источник состава: нет предыдущего публично доступного состава.");
        }

        builder.AppendLine();

        // Determine period for header
        var period = targetEventId <= FplChipSeasonRules.FirstHalfLastEvent ? "первой половины" : "второй половины";
        builder.Append("**Фишки ");
        builder.Append(period);
        builder.AppendLine("**");

        foreach (var chip in manager.Chips.OrderBy(c => c.Chip))
        {
            var display = ChipDisplay[chip.Chip];
            builder.Append(display.Emoji);
            builder.Append(' ');
            builder.Append(display.Name);
            builder.Append(" — ");
            if (chip.IsAvailable)
            {
                builder.Append("✅ доступен");
                if (chip.Urgency == FplChipUrgency.Critical)
                {
                    builder.Append(" ⚠️ сгорит после GW");
                    builder.Append(targetEventId == FplChipSeasonRules.FirstHalfLastEvent
                        ? "19"
                        : finalEventId.ToString(CultureInfo.InvariantCulture));
                }
                else if (chip.Urgency == FplChipUrgency.High)
                {
                    builder.Append(" ⚠️ скоро сгорит");
                }
                else if (chip.Urgency == FplChipUrgency.Low)
                {
                    builder.Append(" (низкая срочность)");
                }
            }
            else
            {
                switch (chip.UnavailabilityReason)
                {
                    case FplChipUnavailabilityReason.UsedInPeriod when chip.UsedEventId is not null:
                        builder.Append("❌ использован в GW");
                        builder.Append(chip.UsedEventId.Value.ToString(CultureInfo.InvariantCulture));
                        break;
                    case FplChipUnavailabilityReason.ConsecutiveFreeHit when chip.UsedEventId is not null:
                        builder.Append("⏳ временно недоступен; использован в GW");
                        builder.Append(chip.UsedEventId.Value.ToString(CultureInfo.InvariantCulture));
                        break;
                    case FplChipUnavailabilityReason.OpeningGameweek:
                        builder.Append("❌ недоступен — открытие сезона");
                        break;
                    case FplChipUnavailabilityReason.EntryMetadataUnavailable:
                        builder.Append("❌ недоступен — данные о старте сезона недоступны");
                        break;
                    case FplChipUnavailabilityReason.AnotherChipActive when chip.BlockingChip is not null:
                        builder.Append("❌ недоступен — другая фишка активна (");
                        builder.Append(ChipDisplay[chip.BlockingChip.Value].Name);
                        builder.Append(" в GW");
                        builder.Append((chip.UsedEventId ?? targetEventId).ToString(CultureInfo.InvariantCulture));
                        builder.Append(')');
                        break;
                    default:
                        if (chip.UsedEventId is not null)
                        {
                            if (chip.Chip == FplChipType.FreeHit && chip.UsedEventId == targetEventId - 1)
                            {
                                builder.Append("⏳ временно недоступен; использован в GW");
                                builder.Append(chip.UsedEventId.Value.ToString(CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append("❌ использован в GW");
                                builder.Append(chip.UsedEventId.Value.ToString(CultureInfo.InvariantCulture));
                            }
                        }
                        else
                        {
                            builder.Append("❌ недоступен");
                        }

                        break;
                }
            }

            builder.AppendLine();
        }

        if (manager.Chips.Count == 0 && !manager.ChipHistoryAvailable)
        {
            builder.AppendLine("⚠️ История фишек недоступна.");
        }

        builder.AppendLine();

        // Free Hit recommendation section
        var freeHitChip = manager.Chips.FirstOrDefault(c => c.Chip == FplChipType.FreeHit);

        if (!manager.ChipHistoryAvailable)
        {
            builder.AppendLine("🎯 **Free Hit: данные недоступны**");
            builder.AppendLine("Не удалось загрузить историю фишек, поэтому рекомендация недоступна.");
        }
        else if (freeHitChip is not null && !freeHitChip.IsAvailable)
        {
            switch (freeHitChip.UnavailabilityReason)
            {
                case FplChipUnavailabilityReason.OpeningGameweek:
                    builder.AppendLine("🎯 Free Hit — недоступен — открытие сезона.");
                    break;
                case FplChipUnavailabilityReason.EntryMetadataUnavailable:
                    builder.AppendLine("🎯 Free Hit — недоступен — данные о старте сезона недоступны.");
                    break;
                case FplChipUnavailabilityReason.ConsecutiveFreeHit when freeHitChip.UsedEventId is not null:
                    builder.Append("🎯 Free Hit — временно недоступен; использован в GW");
                    builder.Append(freeHitChip.UsedEventId.Value.ToString(CultureInfo.InvariantCulture));
                    builder.AppendLine();
                    break;
                case FplChipUnavailabilityReason.AnotherChipActive when freeHitChip.BlockingChip is not null:
                    builder.Append("🎯 Free Hit — недоступен — другая фишка активна (");
                    builder.Append(ChipDisplay[freeHitChip.BlockingChip.Value].Name);
                    builder.Append(" в GW");
                    builder.Append((freeHitChip.UsedEventId ?? targetEventId).ToString(CultureInfo.InvariantCulture));
                    builder.AppendLine(").");
                    break;
                case FplChipUnavailabilityReason.UsedInPeriod when freeHitChip.UsedEventId is not null:
                    var usedInFirstHalf = freeHitChip.UsedEventId <= FplChipSeasonRules.FirstHalfLastEvent;
                    var targetInFirstHalf = targetEventId <= FplChipSeasonRules.FirstHalfLastEvent;
                    if (usedInFirstHalf && targetInFirstHalf)
                    {
                        builder.AppendLine("🎯 Free Hit — уже использован в первой половине сезона.");
                    }
                    else if (!usedInFirstHalf && !targetInFirstHalf)
                    {
                        builder.AppendLine("🎯 Free Hit — уже использован во второй половине сезона.");
                    }
                    else
                    {
                        builder.Append("🎯 Free Hit — уже использован в GW");
                        builder.Append(freeHitChip.UsedEventId.Value.ToString(CultureInfo.InvariantCulture));
                        builder.AppendLine();
                    }

                    break;
                default:
                    if (freeHitChip.UsedEventId is not null)
                    {
                        var usedInFirstHalf2 = freeHitChip.UsedEventId <= FplChipSeasonRules.FirstHalfLastEvent;
                        var targetInFirstHalf2 = targetEventId <= FplChipSeasonRules.FirstHalfLastEvent;
                        if (usedInFirstHalf2 && targetInFirstHalf2)
                        {
                            builder.AppendLine("🎯 Free Hit — уже использован в первой половине сезона.");
                        }
                        else if (!usedInFirstHalf2 && !targetInFirstHalf2)
                        {
                            builder.AppendLine("🎯 Free Hit — уже использован во второй половине сезона.");
                        }
                        else if (freeHitChip.UsedEventId == targetEventId - 1)
                        {
                            builder.Append("🎯 Free Hit — временно недоступен; использован в GW");
                            builder.Append(freeHitChip.UsedEventId.Value.ToString(CultureInfo.InvariantCulture));
                            builder.AppendLine();
                        }
                        else
                        {
                            builder.Append("🎯 Free Hit — уже использован в GW");
                            builder.Append(freeHitChip.UsedEventId.Value.ToString(CultureInfo.InvariantCulture));
                            builder.AppendLine();
                        }
                    }
                    else
                    {
                        builder.AppendLine("🎯 Free Hit — недоступен в этом туре.");
                    }

                    break;
            }
        }
        else if (!manager.SquadAvailable)
        {
            builder.AppendLine("🎯 **Free Hit: данные о составе недоступны**");
            builder.AppendLine("Не удалось загрузить последний публично доступный состав.");
        }
        else if (manager.FreeHitRecommendation is null)
        {
            builder.AppendLine("🎯 **Free Hit: 0/100**");
            builder.AppendLine("Явного повода использовать Free Hit в этом туре нет.");
        }
        else
        {
            var rec = manager.FreeHitRecommendation;
            builder.Append("🎯 **Free Hit: ");
            builder.Append(rec.OpportunityScore.ToString(CultureInfo.InvariantCulture));
            builder.Append("/100");
            if (rec.Level != FplChipOpportunityLevel.None)
            {
                builder.Append(" — ");
                builder.Append(GetLevelWording(rec.Level));
            }

            builder.AppendLine("**");
            builder.AppendLine();

            if (rec.Reasons.Count == 0)
            {
                builder.AppendLine("Явного повода использовать Free Hit в этом туре нет.");
            }
            else
            {
                foreach (var reason in rec.Reasons)
                {
                    builder.Append("• ");
                    builder.Append(FormatReason(reason));
                    builder.AppendLine();
                }
            }

            if (rec.Level == FplChipOpportunityLevel.None || rec.Level == FplChipOpportunityLevel.Consider)
            {
                // If expiry critical but opportunity low, add note
                if (freeHitChip is not null && (freeHitChip.Urgency == FplChipUrgency.High || freeHitChip.Urgency == FplChipUrgency.Critical))
                {
                    builder.AppendLine();
                    builder.AppendLine("⚠️ Но Free Hit всё ещё не использована и скоро сгорит.");
                    if (targetEventId == FplChipSeasonRules.FirstHalfLastEvent)
                    {
                        builder.AppendLine("🚨 Первая Free Hit сгорит после дедлайна GW19.");
                    }
                }
                else if (rec.Level == FplChipOpportunityLevel.None)
                {
                    builder.AppendLine("Явного повода использовать Free Hit в этом туре нет.");
                }
            }
        }

        builder.AppendLine();
        if (sourceEventId is not null)
        {
            builder.Append("⚠️ Состав мог измениться после дедлайна GW");
            builder.Append(sourceEventId.Value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(".");
            builder.Append("Трансферы на GW");
            builder.Append(targetEventId.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(" до дедлайна публично не видны.");
        }
        else
        {
            builder.AppendLine("⚠️ Нет данных для анализа состава; дождитесь первого дедлайна.");
        }

        // Expiry warning for other chips
        var urgentChips = manager.Chips.Where(c => c.IsAvailable && (c.Urgency == FplChipUrgency.High || c.Urgency == FplChipUrgency.Critical)).ToList();
        if (urgentChips.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("⚠️ **Фишки скоро сгорят**");
            foreach (var chip in urgentChips)
            {
                var display = ChipDisplay[chip.Chip];
                builder.Append(display.Emoji);
                builder.Append(' ');
                builder.Append(display.Name);
                builder.AppendLine();
            }

            if (targetEventId == FplChipSeasonRules.FirstHalfLastEvent)
            {
                builder.AppendLine("🚨 Эти фишки сгорят после дедлайна GW19.");
            }
        }

        return builder.ToString().TrimEnd();
    }

    public string ComposeScheduledDigest(FplChipWatchReport report) =>
        ComposeScheduledDigest(report, new FplChipWatchOptions());

    public string ComposeScheduledDigest(FplChipWatchReport report, FplChipWatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        var builder = new StringBuilder();
        builder.Append("🧠 **Chip Watch — GW");
        builder.Append(report.TargetEventId.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("**");
        builder.AppendLine();

        if (report.SourceSquadEventId is not null)
        {
            builder.Append("_Анализ по последним публично доступным составам из GW");
            builder.Append(report.SourceSquadEventId.Value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("._");
        }

        builder.AppendLine();

        // Free Hit section - only notification-worthy (score >= threshold and Strong/VeryStrong)
        var strongManagers = report.Managers
            .Where(m => m.FreeHitRecommendation is not null &&
                        m.FreeHitRecommendation.OpportunityScore >= options.MinimumNotificationScore &&
                        m.FreeHitRecommendation.Level is FplChipOpportunityLevel.Strong or FplChipOpportunityLevel.VeryStrong)
            .OrderByDescending(m => m.FreeHitRecommendation!.OpportunityScore)
            .ThenBy(m => m.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.EntryId)
            .ToList();

        if (strongManagers.Count > 0)
        {
            builder.AppendLine("🎯 **Free Hit**");
            builder.AppendLine();
            foreach (var manager in strongManagers)
            {
                var rec = manager.FreeHitRecommendation!;
                var icon = rec.Level == FplChipOpportunityLevel.VeryStrong ? "🔥" : "👍";
                builder.Append(icon);
                builder.Append(' ');
                builder.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
                builder.Append(" — ");
                builder.Append(rec.OpportunityScore.ToString(CultureInfo.InvariantCulture));
                builder.Append("/100");
                builder.AppendLine();
                foreach (var reason in rec.Reasons)
                {
                    builder.Append("• ");
                    builder.Append(FormatReason(reason));
                    builder.AppendLine();
                }

                builder.AppendLine();
            }
        }

        // Expiry section - only High/Critical
        var expiryManagers = report.Managers
            .Select(m => new
            {
                Manager = m,
                UrgentChips = m.Chips.Where(c => c.IsAvailable && (c.Urgency == FplChipUrgency.High || c.Urgency == FplChipUrgency.Critical)).ToList(),
                MaxUrgency = m.Chips.Where(c => c.IsAvailable).Select(c => c.Urgency).DefaultIfEmpty(FplChipUrgency.None).Max()
            })
            .Where(x => x.UrgentChips.Count > 0)
            .OrderByDescending(x => x.MaxUrgency)
            .ThenBy(x => x.Manager.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Manager.EntryId)
            .ToList();

        if (expiryManagers.Count > 0)
        {
            builder.AppendLine("⚠️ **Фишки скоро сгорят**");
            builder.AppendLine();
            foreach (var item in expiryManagers)
            {
                builder.Append(DiscordTextSafety.SanitizeExternalName(item.Manager.EntryName));
                builder.AppendLine(":");
                foreach (var chip in item.UrgentChips.OrderBy(c => c.Chip))
                {
                    var display = ChipDisplay[chip.Chip];
                    builder.Append(display.Emoji);
                    builder.Append(' ');
                    builder.Append(display.Name);
                    builder.AppendLine();
                }

                builder.AppendLine();
            }

            if (report.TargetEventId == FplChipSeasonRules.FirstHalfLastEvent)
            {
                builder.AppendLine("🚨 Первая половина сезона заканчивается после GW19.");
                builder.AppendLine();
            }
            else if (report.TargetEventId == report.FinalEventId &&
                     expiryManagers.Any(x => x.UrgentChips.Any(c => c.Urgency == FplChipUrgency.Critical)))
            {
                builder.AppendLine("🚨 Эти фишки сгорят после дедлайна GW" + report.FinalEventId.ToString(CultureInfo.InvariantCulture) + ".");
                builder.AppendLine();
            }
        }

        if (strongManagers.Count == 0 && expiryManagers.Count == 0)
        {
            // This should not be called when no signals; but handle
            builder.Append("Сильных сигналов для Chip Watch на GW");
            builder.Append(report.TargetEventId.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(" не найдено.");
        }

        builder.AppendLine("Подробности: `/chipwatch`");

        return builder.ToString().TrimEnd();
    }

    public string ComposeNotFound(string query)
    {
        return $"🔍 Команда \"{DiscordTextSafety.SanitizeExternalName(query)}\" не найдена.";
    }

    public string ComposeAmbiguous(string query, IReadOnlyList<string> candidates)
    {
        var builder = new StringBuilder();
        builder.Append("🔍 Не удалось однозначно найти команду \"");
        builder.Append(DiscordTextSafety.SanitizeExternalName(query));
        builder.AppendLine("\".");
        builder.AppendLine();
        builder.AppendLine("Возможные варианты:");
        foreach (var candidate in candidates)
        {
            builder.Append("• ");
            builder.Append(DiscordTextSafety.SanitizeExternalName(candidate));
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public string ComposeNoUpcomingEvent()
    {
        return "⏸️ Сейчас нет предстоящего тура FPL, поэтому Chip Watch недоступен.";
    }

    private static string FormatReason(FplChipRecommendationReason reason)
    {
        return reason.Kind switch
        {
            FplChipRecommendationReasonKind.BlankPlayers => $"{reason.Count} игрока без матча",
            FplChipRecommendationReasonKind.UnavailablePlayers => reason.Count == 1 ? "1 игрок недоступен" : $"{reason.Count} игрока недоступны",
            FplChipRecommendationReasonKind.DoubtfulPlayers => reason.Count == 1 ? "1 игрок под вопросом" : $"{reason.Count} игрока под вопросом",
            FplChipRecommendationReasonKind.DifficultFixtures => $"{reason.Count} игрока имеют сложный матч",
            _ => $"{reason.Count} игрока"
        };
    }

    private static string GetLevelWording(FplChipOpportunityLevel level)
    {
        return level switch
        {
            FplChipOpportunityLevel.VeryStrong => "очень сильный вариант",
            FplChipOpportunityLevel.Strong => "сильный вариант",
            FplChipOpportunityLevel.Consider => "стоит рассмотреть",
            _ => string.Empty
        };
    }

    private static bool IsSecondHalfCritical(int targetEventId, IReadOnlyList<FplManagerChipWatch> managers)
    {
        // If any manager has critical urgency in second half final GW
        return managers.Any(m => m.Chips.Any(c => c.Urgency == FplChipUrgency.Critical));
    }
}
