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
Bot__FantasyPremierLeague__LiveNotificationCooldownMinutes=60
Bot__FantasyPremierLeague__SignificantLiveRankChange=2
Bot__FantasyPremierLeague__AutomaticSubstitutionHighlightPoints=5
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
Bot__Schedules__FplChipWatch__Enabled=true
Bot__Schedules__FplChipWatch__Cron=0 0 6,12,18 * * ?
Bot__FantasyPremierLeague__ChipWatch__MinimumNotificationScore=60
Bot__FantasyPremierLeague__ChipWatch__NotificationWindowStartHours=36
Bot__FantasyPremierLeague__ChipWatch__NotificationWindowEndHours=18
Bot__Notifications__Targets__0__GuildId=<FIRST-GUILD-ID>
Bot__Notifications__Targets__0__ChannelId=<FIRST-CHANNEL-ID>
Bot__Notifications__Targets__0__MentionEveryone=false
```

## AutonomousWork dispatcher trigger

The optional `AutonomousWorkDispatcher` uses the existing Quartz scheduler as a
reliable clock for AutonomousWork. When enabled, a Quartz job fires every 10
minutes and sends one GitHub workflow-dispatch request for
`AlexBDevCorner/AutonomousWork/.github/workflows/dispatch.yml` on `master`.
Task selection, claiming, reconciliation, retries, review handling, and
target-repository dispatch remain owned by AutonomousWork. The trigger never
inspects tasks and does not depend on Discord connectivity.

Tracked `appsettings.json` keeps the trigger disabled with safe defaults:

```json
"AutonomousWorkDispatcher": {
  "Enabled": false,
  "Cron": "0 0/10 * * * ?",
  "Owner": "AlexBDevCorner",
  "Repository": "AutonomousWork",
  "Workflow": "dispatch.yml",
  "Ref": "master"
}
```

No token is committed. To enable, configure the non-secret values and supply
the token only through the runtime secret provider:

```dotenv
Bot__AutonomousWorkDispatcher__Enabled=true
Bot__AutonomousWorkDispatcher__Cron=0 0/10 * * * ?
Bot__AutonomousWorkDispatcher__Owner=AlexBDevCorner
Bot__AutonomousWorkDispatcher__Repository=AutonomousWork
Bot__AutonomousWorkDispatcher__Workflow=dispatch.yml
Bot__AutonomousWorkDispatcher__Ref=master
Bot__AutonomousWorkDispatcher__Token=<AUTONOMOUSWORK-DISPATCH-TOKEN>
```

The token is a fine-grained token scoped only to the private
`AlexBDevCorner/AutonomousWork` repository with the minimum permission required
to create a workflow dispatch event. Startup validation requires a valid Quartz
cron always, and requires owner, repository, workflow, ref, and token when
enabled. A disabled configuration does not require a token. A failed GitHub
request is logged without the token, fails that Quartz execution, and leaves
future 10-minute occurrences unchanged; there is no fast retry loop.

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
are logged and skipped without disconnecting the bot. The message includes a
short onboarding checklist, the three league join links, and `/help` for the
full command guide.

Application-command registration publishes `/help`, `/prices`, `/deadline`,
`/live`, `/standings`, `/profile`, `/achievements`, `/benchleague`, `/chips`, `/chipwatch`,
and `/hugme`. `/deadline` includes exact Riga civil time for both FPL and UCL
plus a Discord relative timestamp that keeps counting down in the client.

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

The optional `FplLiveInsights` job is disabled by default. When enabled, it polls at
the configured cron interval. The default `0 0/15 * * * ?` schedule gives it four
observations per hour. Polling frequency and publication frequency are separate.
The first observation of each gameweek establishes a baseline and sends nothing.
Later observations compare the current state with the immediately preceding state.

The scheduled job creates highlights only when the league leader changes, a manager
moves by the configured number of places, a bench or captain-success threshold is
crossed, a captain disaster becomes final, or an automatic substitution saves enough
points. It stores the observed snapshot, pending highlights, digest sequence, and
per-target cooldown in `data/fpl-live.db`. Restarting the bot therefore does not reset
the cooldown or replay acknowledged highlights.

A leader highlight requires one unique leader to be replaced by another unique
leader. Transitions into or out of a tie for first stay silent. The reported lead is
the score difference from the highest strictly lower score group, so several managers
can share second place.

The first meaningful change after the baseline can publish on the next scheduler
execution. A successful publication starts the per-target cooldown. Highlights found
during that cooldown are merged into one short digest. Repeated bench, captain, rank,
and leader changes replace the pending value instead of adding duplicate lines. A
failed Discord send leaves the digest pending and does not start the cooldown. At the
default settings, each target receives at most one successful live digest in any
rolling 60-minute period.

Removing a Discord target from configuration stops new highlights and publications
for that target, but keeps its publication sequence and pending state in SQLite. If
the target is added again during the same gameweek, its next digest continues with a
new source identifier instead of colliding with an earlier delivery checkpoint.

Routine score changes do not create highlights by themselves. Fixture start and end,
`Playing` or `YetToPlay` count changes, rank movement below the configured threshold,
standings metadata, and changing swing insights also remain silent. With the default
threshold, a one-place move stays silent.

The `/live` slash command is independent of this state. It always calculates and
returns the complete current dashboard with no cooldown and no dependency on prior
notifications. Configure the live thresholds explicitly when changing the defaults:

- `LargeBenchPointsThreshold` is the total raw points left on a manager's bench.
- `CaptainSuccessEffectivePointsThreshold` is the captain's multiplied points.
- `CaptainDisasterPointsThreshold` is the maximum raw captain points for a disaster.
- `CaptainDisasterViceCaptainPointsThreshold` is the minimum raw vice-captain points
  required for the same alert.
- `LiveNotificationCooldownMinutes` is the successful-publication cooldown for each
  Discord target. The default is `60`.
- `SignificantLiveRankChange` is the minimum movement between observations. The
  default is `2` places.
- `AutomaticSubstitutionHighlightPoints` is the minimum saved-points value for an
  automatic-substitution highlight. The default is `5`.

## FPL player price changes

The optional `FplPriceChanges` job is disabled by default. When enabled, it checks
the current price of every FPL player at the top of each hour from 12:00 through
22:00 in `Europe/Riga`. It stores the latest successful snapshot in
`data/fpl-price-snapshot.db` and publishes every detected player price change
since the previous notification. For the configured classic league, the job
loads the current public squad for each manager, annotates players owned in the
league with their owners, and totals the value change per team. League totals
and per-team impacts are based only on players actually owned by those teams,
while unowned players remain visible so managers can spot market opportunities.
Squad lookups run at most five at a time to avoid an API request burst in large
leagues. If none of the changed players are owned in the league, all market
changes are still listed with a note that league squads were not affected. If
only some squads load, the message reports its coverage; if league data is
unavailable, it falls back to the global changes instead of losing the
notification. The first run establishes the baseline and does not publish a
message.

The last successfully published change batch is stored alongside the current
price baseline. `/prices` checks for newer changes without advancing that
baseline, then shows either the new batch or the last published batch with
ownership from that batch's gameweek. This keeps the command useful between
scheduled checks and does not consume a change before the scheduled notification
can publish it.

## FPL Chip Watch

The optional `FplChipWatch` job is disabled by default. When enabled, it analyses the configured classic league's managers for chip availability and Free Hit opportunities. It uses the public FPL entry history (`/api/entry/{id}/history/`), the latest publicly visible squad (`/api/entry/{id}/event/{id}/picks/`), bootstrap-static, classic standings, and target-GW fixtures. No authenticated FPL session, external prediction API, or AI service is used; all calculations are deterministic.

- **Chip sets**: first half (GW1–19) and second half (GW20–end). An unused chip from the first half expires after GW19. Final GW is derived from bootstrap-static.
- **Restrictions**: Wildcard and Free Hit cannot be used in GW1; Free Hit cannot be used in consecutive Gameweeks (e.g., used in GW19 → not available in GW20, next available GW21).
- **Expiry urgency**: GW17 Low, GW18 High, GW19 Critical for first half; `final-2` Low, `final-1` High, `final` Critical for second half. Only High/Critical trigger scheduled notifications; Low is shown interactively.
- **Free Hit scoring** (0–100, deterministic): blanks dominate (+12 first 3, +8 additional, cap 60), unavailable non-blank +8 (cap 24), doubtful non-blank +4 (cap 12), difficult fixtures (all fixtures difficulty ≥4) +3 (cap 15). If `blank < 3`, score is capped at 59 so ordinary difficult fixtures cannot create a strong recommendation.
- **Levels**: 0–39 None, 40–59 Consider, 60–74 Strong, 75–100 VeryStrong. Only Strong/VeryStrong qualify for scheduled alerts.
- **Squad source**: the newest event before the target whose deadline has already passed (derived from `bootstrap.Events` and `TimeProvider`), never `target-1` blindly. For GW1 no previous public squad exists and only chip expiry is shown. User-facing output explicitly states the source GW and warns that transfers for the upcoming GW are not visible before the deadline.
- **Scheduled digest**: configurable window `Bot:FantasyPremierLeague:ChipWatch:NotificationWindowStartHours` (default 36) to `NotificationWindowEndHours` (default 18) before the deadline, polling at `Bot:Schedules:FplChipWatch:Cron` (default `0 0 6,12,18 * * ?` in `Europe/Riga`). No message is sent when there are no Strong/VeryStrong Free Hit signals or High/Critical expiry warnings. At most one digest per Discord target per Gameweek is sent via the existing notification checkpoint store (`fpl-chip-watch` type, identifier `fpl-chip-watch-gw-{id}`); failed targets can be retried without duplicating successful ones. Chip Watch never uses `@everyone`, even when `MentionEveryone=true`.
- **Commands**: `/chipwatch` shows league overview; `/chipwatch team:<name>` shows manager details with exact/unique partial matching (team name, manager name) and sanitized external names. `/chips` remains the static guide.

Key environment variables:

```dotenv
Bot__Schedules__FplChipWatch__Enabled=false
Bot__Schedules__FplChipWatch__Cron=0 0 6,12,18 * * ?
Bot__FantasyPremierLeague__ChipWatch__MinimumNotificationScore=60
Bot__FantasyPremierLeague__ChipWatch__NotificationWindowStartHours=36
Bot__FantasyPremierLeague__ChipWatch__NotificationWindowEndHours=18
```

Smart scoring in v1 covers Free Hit only; Bench Boost, Triple Captain and Wildcard expiry is tracked but their recommendation models are future work. All external FPL names are sanitized with `DiscordTextSafety.SanitizeExternalName`, and messages respect Discord's 2,000-character limit with bounded concurrency (5) for manager data.

## EventWatch (Riga FC ticket monitoring)

The optional `EventWatch` capability checks Riga FC's official website every
10 minutes for ticket-sale evidence and sends Discord alerts exactly once.
It is a separate domain from FPL/UCL (`DiscordBot.EventWatch`) and reuses the
existing `IDiscordNotificationPublisher` checkpoints, so no new database table
is required.

Tracked `appsettings.json` keeps the feature disabled with safe defaults and
no real Discord IDs:

```json
"Schedules": {
  "EventWatch": {
    "Enabled": false,
    "Cron": "0 0/10 * * * ?"
  }
},
"EventWatch": {
  "Watches": [
    {
      "Enabled": false,
      "Id": "riga-fc-atalanta-2026",
      "Title": "Riga FC vs Atalanta tickets",
      "MatchTerms": [ "Atalanta" ],
      "Targets": [
        {
          "GuildId": 0,
          "ChannelId": 0,
          "MentionEveryone": true
        }
      ]
    }
  ]
}
```

The initial watch `riga-fc-atalanta-2026` searches Riga FC's homepage
(`https://rigafc.lv/`), calendar (`https://rigafc.lv/kalendars/`), and news
(`https://rigafc.lv/jaunumi/`) for case-insensitive `Atalanta` mentions
combined with ticket-sale wording (`biļete`, `biļetes`, `biļešu`, `ticket`,
`tickets`, `pārdošanā`, `iegādāties`, `pirkt`) or an actionable ticket link in
the same content block. A plain fixture, a historical mention, a generic
site-wide `Biļetes` navigation link, or unrelated page changes do not notify.
When the same block proves both, both `event-watch-announcement` and
`event-watch-ticket-link` are delivered once per configured target; repeats
are suppressed by the existing notification checkpoints.

Each watch owns its dedicated Discord targets. EventWatch never publishes to
the general `Bot:Notifications:Targets` list, so the ticket alert can use a
different channel from normal bot notifications. `MentionEveryone` is
configured independently per EventWatch target and is permitted for the two
`event-watch-*` types.

To enable in deployment, set the schedule plus the watch and its dedicated
target (real IDs belong only in runtime configuration):

```dotenv
Bot__Schedules__EventWatch__Enabled=true
Bot__Schedules__EventWatch__Cron=0 0/10 * * * ?
Bot__EventWatch__Watches__0__Enabled=true
Bot__EventWatch__Watches__0__Id=riga-fc-atalanta-2026
Bot__EventWatch__Watches__0__Title=Riga FC vs Atalanta tickets
Bot__EventWatch__Watches__0__MatchTerms__0=Atalanta
Bot__EventWatch__Watches__0__Targets__0__GuildId=<EVENTWATCH-GUILD-ID>
Bot__EventWatch__Watches__0__Targets__0__ChannelId=<EVENTWATCH-CHANNEL-ID>
Bot__EventWatch__Watches__0__Targets__0__MentionEveryone=true
```

Disabling `Bot__Schedules__EventWatch__Enabled` removes the scheduled
EventWatch job on the next process start and leaves all other jobs unchanged.
Do not poll Biļešu Serviss directly; the detection path is the official Riga
FC website, although a detected outbound ticket URL is included in the Discord
message.

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
(`fpl-deadline-*` and `ucl-deadline-*`) plus the EventWatch alerts
(`event-watch-announcement` and `event-watch-ticket-link`); all other
notification types are sent without a mention. Jobs never enumerate all
guilds and never fall back to channel display names. EventWatch targets are
configured per watch under `Bot:EventWatch:Watches`, not under
`Bot:Notifications:Targets`.
