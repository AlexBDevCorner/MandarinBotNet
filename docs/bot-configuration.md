# Bot configuration

Bot behavior is bound from the `Bot` configuration section and validated when
the host starts. Production refuses to start when credentials are missing, no
job is enabled, an enabled standings job lacks its league ID, a schedule is
invalid, or an enabled job has no explicit notification target. This prevents a
partially configured deployment from appearing healthy while doing the wrong
work.

The tracked `appsettings.json` contains only non-secret schedule defaults. All
jobs and command registration are disabled, and the notification target list is
empty. Welcome messages are also disabled. A checkout therefore cannot
broadcast or mutate Discord merely because someone supplies a token.

## Discord member intent

Welcome messages use Discord's guild-member join event. The application enables
Discord.NET's `GuildMembers` gateway intent in code, but Discord must also permit
the privileged intent for the bot application. In the Discord Developer Portal,
open **Applications > your application > Bot > Privileged Gateway Intents** and
enable **Server Members Intent** before enabling welcome messages. Without both
sides configured, Discord will not deliver member-join events.

## Production environment

Supply secrets and deployment-specific identifiers through environment
variables or another configuration provider. A Docker env file enabling all
jobs for one target looks like this:

```dotenv
Bot__Discord__Token=<DISCORD-BOT-TOKEN>
Bot__Discord__ReadinessTimeout=00:00:30
Bot__Discord__Commands__RegistrationMode=Global
Bot__WelcomeMessages__Enabled=true
Bot__WelcomeMessages__GuildId=<WELCOME-GUILD-ID>
Bot__WelcomeMessages__ChannelId=<WELCOME-CHANNEL-ID>
Bot__FantasyPremierLeague__ClassicLeagueId=<CLASSIC-LEAGUE-ID>
Bot__FantasyPremierLeague__HeadToHeadLeagueId=<HEAD-TO-HEAD-LEAGUE-ID>
Bot__FantasyPremierLeague__MaxStandingsPages=10
Bot__Schedules__TimeZoneId=Europe/Riga
Bot__Schedules__PremierLeagueNotifications__Enabled=true
Bot__Schedules__PremierLeagueNotifications__Cron=0 0 * * * ?
Bot__Schedules__UclFantasyNotifications__Enabled=true
Bot__Schedules__UclFantasyNotifications__Cron=0 0 * * * ?
Bot__Schedules__ClassicStandings__Enabled=true
Bot__Schedules__ClassicStandings__Cron=0 0 17 * * ?
Bot__Schedules__HeadToHeadStandings__Enabled=true
Bot__Schedules__HeadToHeadStandings__Cron=0 0 17 * * ?
Bot__Schedules__BenchWarmingLeague__Enabled=true
Bot__Schedules__BenchWarmingLeague__Cron=0 0 18 * * ?
Bot__Notifications__Targets__0__GuildId=<FIRST-GUILD-ID>
Bot__Notifications__Targets__0__ChannelId=<FIRST-CHANNEL-ID>
Bot__Notifications__Targets__0__MentionEveryone=false
```

Keep `Bot__Discord__Token` only in the VM env file or the deployment platform's
secret provider. Never put it in `appsettings.json`, source control, logs, or a
container image.

`Bot__WelcomeMessages__Enabled` is an explicit opt-in. When enabled, only joins
in `Bot__WelcomeMessages__GuildId` are handled, and the welcome text plus the
packaged `pc7n1.jpg` image are sent together to
`Bot__WelcomeMessages__ChannelId`. Missing or unavailable destination settings
are logged and skipped without disconnecting the bot.

`MaxStandingsPages` bounds the number of FPL standings pages fetched by each
job. The default of 10 represents up to 500 league entries while preventing a
bad or unexpectedly large upstream pagination sequence from running forever.

## Bench warming league

The bench warming league is an alternative standings table tracking fantasy
points league managers leave on the bench. Once a gameweek finishes, the
`BenchWarmingLeague` job fetches every classic-league entry's final lineup,
sums the points of players who stayed benched (multiplier `0` after automatic
substitutions), and persists per-round results into a separate SQLite database
(`data/bench-warming-league.db` next to the notification database). Standings
accumulate per manager across the season and reset naturally when a new FPL
season starts because every row is keyed by the season name.

The job requires `Bot:FantasyPremierLeague:ClassicLeagueId` and posts a round
summary to every notification target after calculating a new round. The
`/benchleague` slash command shows the current season standings on demand.
Historical rounds are not backfilled; tracking starts when the feature ships.

## Multiple targets and mentions

Targets are addressed only by Discord snowflake IDs. Add another independently
configured target by incrementing the array index:

```dotenv
Bot__Notifications__Targets__1__GuildId=<SECOND-GUILD-ID>
Bot__Notifications__Targets__1__ChannelId=<SECOND-CHANNEL-ID>
Bot__Notifications__Targets__1__MentionEveryone=true
```

`MentionEveryone` defaults to `false` for every target. Set it to `true` only
where broadcasts are intentional and the bot has permission to mention
everyone. Jobs never enumerate all guilds and never fall back to channel display
names.
