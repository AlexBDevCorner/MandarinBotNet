using Quartz;
using TimeZoneConverter;

namespace DiscordBot.Jobs
{
    public static class JobSchedules
    {
        public const string RigaTimeZoneId = "Europe/Riga";
        public const string PremierLeagueNotificationCron = "0 0 * * * ?";
        public const string StandingsCron = "0 0 17 * * ?";
        public const string PremierLeagueNotificationTriggerName =
            "PremierLeagueNotificationTrigger";
        public const string PremierLeagueClassicStandingsInformationTriggerName =
            "PremierLeagueClassicStandingsInformationTrigger";
        public const string PremierLeagueH2hStandingsInformationTriggerName =
            "PremierLeagueH2hStandingsInformationTrigger";

        public static readonly JobKey PremierLeagueNotificationJobKey =
            new("PremierLeagueNotification");

        public static readonly JobKey PremierLeagueClassicStandingsInformationJobKey =
            new("PremierLeagueClassicStandingsInformation");

        public static readonly JobKey PremierLeagueH2hStandingsInformationJobKey =
            new("PremierLeagueH2hStandingsInformation");

        public static TimeZoneInfo RigaTimeZone =>
            TZConvert.GetTimeZoneInfo(RigaTimeZoneId);

        public static ITrigger CreatePremierLeagueNotificationTrigger() =>
            CreateCronTrigger(
                PremierLeagueNotificationJobKey,
                PremierLeagueNotificationTriggerName,
                PremierLeagueNotificationCron);

        public static ITrigger CreatePremierLeagueClassicStandingsInformationTrigger() =>
            CreateCronTrigger(
                PremierLeagueClassicStandingsInformationJobKey,
                PremierLeagueClassicStandingsInformationTriggerName,
                StandingsCron);

        public static ITrigger CreatePremierLeagueH2hStandingsInformationTrigger() =>
            CreateCronTrigger(
                PremierLeagueH2hStandingsInformationJobKey,
                PremierLeagueH2hStandingsInformationTriggerName,
                StandingsCron);

        public static IReadOnlyList<ITrigger> CreateAllTriggers() =>
        [
            CreatePremierLeagueNotificationTrigger(),
            CreatePremierLeagueClassicStandingsInformationTrigger(),
            CreatePremierLeagueH2hStandingsInformationTrigger()
        ];

        private static ITrigger CreateCronTrigger(
            JobKey jobKey,
            string triggerName,
            string cronExpression)
        {
            return TriggerBuilder.Create()
                .ForJob(jobKey)
                .WithIdentity(triggerName)
                .WithCronSchedule(
                    cronExpression,
                    schedule => schedule
                        .InTimeZone(RigaTimeZone)
                        // Missed runs are stale; resume with the next scheduled occurrence.
                        .WithMisfireHandlingInstructionDoNothing())
                .Build();
        }
    }
}
