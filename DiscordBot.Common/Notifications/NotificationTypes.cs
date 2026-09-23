namespace DiscordBot.Notifications
{
    public static class NotificationTypes
    {
        public const string Deadline24Hours = "fpl-deadline-24-hours";
        public const string Deadline1Hour = "fpl-deadline-1-hour";
        public const string UclDeadline24Hours = "ucl-deadline-24-hours";
        public const string UclDeadline1Hour = "ucl-deadline-1-hour";
        public const string ClassicStandings = "fpl-classic-standings";
        public const string HeadToHeadStandings = "fpl-head-to-head-standings";
        public const string BenchWarmingStandings = "fpl-bench-warming-standings";
        public const string FplGameweekRecap = "fpl-gameweek-recap";
        public const string FplLiveInsights = "fpl-live-insights";
        public const string FplPriceChanges = "fpl-price-changes";
        public const string FplChipWatch = "fpl-chip-watch";
        public const string EventWatchAnnouncement = "event-watch-announcement";
        public const string EventWatchTicketLink = "event-watch-ticket-link";
        public const string EventWatchTicketAvailable = "event-watch-ticket-available";

        private static readonly HashSet<string> EveryoneMentionAllowedTypes =
        [
            Deadline24Hours,
            Deadline1Hour,
            UclDeadline24Hours,
            UclDeadline1Hour,
            EventWatchAnnouncement,
            EventWatchTicketLink,
            EventWatchTicketAvailable
        ];

        public static bool AllowsEveryoneMention(string notificationType) =>
            EveryoneMentionAllowedTypes.Contains(notificationType);
    }
}
