namespace DiscordBot
{
    internal static class DateTimeUtility
    {
        public static TimeZoneInfo GmtPlus3Zone => TimeZoneInfo.CreateCustomTimeZone("GMT+3", TimeSpan.FromHours(3), "GMT+3", "GMT+3");

        public static string GenerateRemainingDaysMessageInRussian(TimeSpan difference)
        {
            string[] dayWordForms = ["дней", "день", "дня"];
            string[] hoursWordForms = ["часов", "час", "часа"];
            string[] minutesWordForms = ["минут", "минута", "минуты"];

            var days = dayWordForms[GetWordForm(difference.Days)];
            var hours = hoursWordForms[GetWordForm(difference.Hours)];
            var minutes = minutesWordForms[GetWordForm(difference.Minutes)];

            return $"{difference.Days} {days}, {difference.Hours} {hours}, {difference.Minutes} {minutes}";
        }

        private static int GetWordForm(int number)
        {
            if (number % 100 >= 11 && number % 100 <= 19)
            {
                return 0;
            }

            int lastDigit = number % 10;

            return lastDigit switch
            {
                1 => 1,
                2 or 3 or 4 => 2,
                _ => 0,
            };
        }
    }
}
