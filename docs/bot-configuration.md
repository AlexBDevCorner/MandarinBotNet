# Bot configuration

Bot behavior is bound from the `Bot` configuration section and validated when
the host starts. Production refuses to start when credentials are missing, no
job is enabled, an enabled league job lacks its league ID, a schedule is
invalid, or an enabled notification job has no explicit notification target. This prevents a
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
Bot__FantasyPremierLeague__RecognitionRuleVersion=v1
Bot__FantasyPremierLeague__LargeBenchPointsThreshold=8
Bot__FantasyPremierLeague__CaptainSuccessEffectivePointsThreshold=20
Bot__FantasyPremierLeague__CaptainDisasterPointsThreshold=2
Bot__FantasyPremierLeague__CaptainDisasterViceCaptainPointsThreshold=8
Bot__FantasyPremierLeague__TransferCostAchievementThreshold=8
Bot__Schedules__TimeZoneId=Europe/Riga
Bot__Schedules__PremierLeagueNotifications__Enabled=true
Bot__Schedules__PremierLeagueNotifications__Cron=0 0/15 * * * ?
Bot__Schedules__UclFantasyNotifications__Enabled=true
Bot__Schedules__UclFantasyNotifications__Cron=0 0/15 * * * ?
Bot__Schedules__ClassicStandings__Enabled=true
Bot__Schedules__ClassicStandings__Cron=0 0 17 * * ?
Bot__Schedules__HeadToHeadStandings__Enabled=true
Bot__Schedules__HeadToHeadStandings__Cron=0 0 17 * * ?
Bot__Schedules__BenchWarmingLeague__Enabled=true
Bot__Schedules__BenchWarmingLeague__Cron=0 0 18 * * ?
Bot__Schedules__FplStatisticsCollection__Enabled=true
Bot__Schedules__FplStatisticsCollection__Cron=0 0 19 * * ?
Bot__Schedules__FplGameweekRecap__Enabled=true
Bot__Schedules__FplGameweekRecap__Cron=0 0 20 * * ?
Bot__Schedules__FplLiveInsights__Enabled=true
Bot__Schedules__FplLiveInsights__Cron=0 0/15 * * * ?
Bot__Schedules__FplPriceChanges__Enabled=true
Bot__Schedules__FplPriceChanges__Cron=0 0 12-22 * * ?
Bot__Notifications__Targets__0__GuildId=<FIRST-GUILD-ID>
Bot__Notifications__Targets__0__ChannelId=<FIRST-CHANNEL-ID>
Bot__Notifications__Targets__0__MentionEveryone=false
```

Deadline notification jobs run every 15 minutes by default. This provides four
delivery attempts during the final 70-minute reminder window if an upstream API,
Discord, or the application is briefly unavailable. Delivery checkpoints still
ensure that each reminder is published only once per event and target. Existing
deployments with hourly `PremierLeagueNotifications` or `UclFantasyNotifications`
cron overrides must remove those overrides or change them to `0 0/15 * * * ?`.

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

## Historical FPL statistics

The `FplStatisticsCollection` job is disabled by default. When enabled, it
records the latest completed gameweek, if it is not already stored, from the
configured classic league into `data/fpl-statistics.db`. Each snapshot is
keyed by season and gameweek and includes manager standings, rank changes,
lineup picks, captaincy, live player points, bench points, transfer-hit costs,
deadlines, and capture metadata. Older gameweeks are not backfilled because the current FPL
standings endpoint returns the current table; collection starts with the
latest completed gameweek available after the feature is enabled. The
collection job does not publish a Discord message and therefore does not
require a notification target. A failed write is logged and rolled back so a
partial snapshot is never exposed to later features.

## FPL gameweek recap

The `FplGameweekRecap` job is disabled by default. When enabled, it makes sure
the latest completed gameweek has a complete historical snapshot, calculates
the winner, score range, average, rank movement, and deterministic awards, then
publishes the recap to every notification target. It can run after the separate
`FplStatisticsCollection` job or collect the missing snapshot itself. Recaps are
checkpointed by season, gameweek, guild, and channel, so retries do not publish
the same target twice. Ties are ordered by current rank, team name, and entry ID;
only the five largest rank movements are listed. Incomplete or unavailable source
data is logged and skipped rather than turned into a misleading message.

Each completed gameweek also evaluates the configured achievement rules and persists
the resulting awards and manager ratings in `data/fpl-recognition.db`. Awards are
keyed by league, season, manager, achievement, and occurrence. Repeatable awards
can occur once per gameweek, while one-time awards are retained for the season and
are not duplicated when the recap job is retried. The first completed recognition
evaluation is also marked for each league, season, and gameweek, so retries return
the persisted result—including an empty award set—instead of applying newer rules
to an already recorded gameweek. Ratings and awards are therefore immutable after
their first write.

The initial rule set is:

- **First Blood** is a one-time award for each manager tied for the highest score in
  the first recorded gameweek of a season.
- **Bench Warmer** is repeatable when a manager leaves at least
  `LargeBenchPointsThreshold` points on the bench.
- **Captain Disaster** is repeatable when the captain scores at most
  `CaptainDisasterPointsThreshold` raw points and the vice-captain scores at least
  `CaptainDisasterViceCaptainPointsThreshold` raw points.
- **Differential Merchant** is repeatable when only one manager in the configured
  league captains that player in the gameweek.
- **-8 Enjoyer** is repeatable when the FPL entry history reports at least
  `TransferCostAchievementThreshold` points in transfer hits.

The Fraud Rating is a deterministic 0-100 score calculated from bench points,
captain versus vice-captain failure gap, transfer-hit cost, and rank falls. Its
component weights are 40, 30, 20, and 10 points respectively, with caps of 10 bench
points, a 10-point captain gap, an 8-point transfer cost, and a five-place rank fall.
The Maguire Index is a deliberately opaque but deterministic 0-100 modulo score
seeded by the league snapshot's entry, event, total score, bench points, transfer
cost, and captaincy points. Both results and their `RecognitionRuleVersion` are
stored so changing thresholds or formulas affects future records without rewriting
historical awards or ratings. The recap publishes the current gameweek's persisted
achievements and ratings.

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

## Live FPL insights

The `/live` slash command calculates the current gameweek view for every entry in
the configured classic league. It combines the active gameweek, current standings,
entry picks, and live player data to show live points and players remaining to play.
The message includes all managers meeting the configured bench threshold, captain
disasters, captain successes, and automatic-substitution salvations. Ties are not
discarded.

The classic league standings `last_updated_data` timestamp is retained as standings
metadata only and never blocks the live calculation. The `/live` command keeps
fetching fixtures, event live data, and manager picks even when the standings
snapshot is old; it only reports failure when the required FPL responses fail or fail
validation. The FPL HTTP client already classifies rate limits and transient upstream
failures for retry; the live service turns an exhausted failure into a safe command
response and skips the scheduled publication.

The optional `FplLiveInsights` job is disabled by default. When enabled, it runs at
the configured cron interval and publishes only once for each event/source update
timestamp per target. Repeated executions with the same source timestamp therefore
do not spam the channel. Configure the alert thresholds explicitly when changing
the defaults:

- `LargeBenchPointsThreshold` is the total raw points left on a manager's bench.
- `CaptainSuccessEffectivePointsThreshold` is the captain's multiplied points.
- `CaptainDisasterPointsThreshold` is the maximum raw captain points for a disaster.
- `CaptainDisasterViceCaptainPointsThreshold` is the minimum raw vice-captain points
  required for the same alert.

## FPL player price changes

The optional `FplPriceChanges` job is disabled by default. When enabled, it checks
the current price of every FPL player at the top of each hour from 12:00 through
22:00 in `Europe/Riga`. It stores the latest successful snapshot in
`data/fpl-price-snapshot.db` and publishes only the players whose prices changed
since the previous notification. The first run establishes the baseline and does
not publish a message.

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
everyone. When enabled, the `@everyone` mention is added **only** for
notification types that are explicitly permitted to broadcast. Currently the
permitted types are the FPL and UCL 24-hour and 1-hour deadline reminders
(`fpl-deadline-*` and `ucl-deadline-*`); all other notification types are sent
without a mention. Jobs never enumerate all guilds and never fall back to
channel display names.
