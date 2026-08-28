# Discord application-command registration

The production bot registers its desired slash commands globally. Global
commands are appropriate because the bot exposes the same command set in every
guild and should not create a separate copy for each guild.

The synchronized command set currently contains:

- `/hugme`, which replies with a hug;
- `/standings`, which defers immediately and then returns on-demand snapshots
  of both configured Fantasy Premier League tables. The command always uses the
  configured classic and head-to-head league IDs and does not accept arbitrary
  league IDs;
- `/deadline`, which shows the next FPL gameweek deadline in Riga time;
- `/benchleague`, which shows the current bench warming league standings from
  the local SQLite storage;
- `/live`, which shows live Fantasy Premier League insights for the configured
  classic league;
- `/profile`, which requires a `manager` string option (team name or manager
  name, matched case-insensitively, with a unique partial match also accepted).
  It reads persisted local season statistics and recognition data for the
  configured classic FPL league and shows the manager's trophy cabinet, tracked
  gameweek wins and personal records. It does not perform arbitrary league
  lookup and does not call the FPL API at command time;
- `/achievements`, which shows the season recognition leaderboards (total
  awards, per-achievement champions and tracked gameweek-win leaders) for the
  configured classic FPL league. It does not perform arbitrary league lookup and
  does not call the FPL API at command time.

Registration runs once after the Discord gateway first becomes ready during a
process startup. It uses Discord's bulk-overwrite endpoint, so removed or
renamed commands are reconciled intentionally from the command definitions in
`DiscordApplicationCommands`. Later gateway reconnects do not register again.
If registration fails, the bot logs the scope, HTTP and Discord error details,
and validation errors, and waits for a process restart before another attempt.

Production uses this configuration:

```json
{
  "Bot": {
    "Discord": {
      "Commands": {
        "RegistrationMode": "Global"
      }
    }
  }
}
```

Development defaults to `Disabled` so starting a local process cannot mutate
production commands accidentally. To test a command with Discord's faster
guild-scoped propagation, provide these environment variables for a development
bot application and test guild:

```dotenv
Bot__Discord__Commands__RegistrationMode=Guild
Bot__Discord__Commands__GuildId=<TEST-GUILD-ID>
```

Use a separate development bot token as `Bot__Discord__Token`; do not point
guild-scoped development registration at the production application.
`RegistrationMode` also accepts `Disabled` when a process should handle
commands without changing their registration.

After starting the development bot, verify `/standings` in the configured test
guild. Guild-scoped commands normally appear much faster than global commands.
Confirm that Discord first shows the deferred response, then displays the
classic section before the head-to-head section. Long standings should continue
in ordered follow-up messages.
