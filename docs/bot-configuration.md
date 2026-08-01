# Bot configuration

Bot behavior is bound from the `Bot` configuration section and validated when
the host starts. Production refuses to start when credentials are missing, no
job is enabled, an enabled standings job lacks its league ID, a schedule is
invalid, or an enabled job has no explicit notification target. This prevents a
partially configured deployment from appearing healthy while doing the wrong
work.

The tracked `appsettings.json` contains only non-secret schedule defaults. All
jobs and command registration are disabled, and the notification target list is
empty. A checkout therefore cannot broadcast or mutate Discord merely because
someone supplies a token.

## Production environment

Supply secrets and deployment-specific identifiers through environment
variables or another configuration provider. A Docker env file enabling all
jobs for one target looks like this:

```dotenv
Bot__Discord__Token=<DISCORD-BOT-TOKEN>
Bot__Discord__ReadinessTimeout=00:00:30
Bot__Discord__Commands__RegistrationMode=Global
Bot__FantasyPremierLeague__ClassicLeagueId=<CLASSIC-LEAGUE-ID>
Bot__FantasyPremierLeague__HeadToHeadLeagueId=<HEAD-TO-HEAD-LEAGUE-ID>
Bot__Schedules__TimeZoneId=Europe/Riga
Bot__Schedules__PremierLeagueNotifications__Enabled=true
Bot__Schedules__PremierLeagueNotifications__Cron=0 0 * * * ?
Bot__Schedules__ClassicStandings__Enabled=true
Bot__Schedules__ClassicStandings__Cron=0 0 17 * * ?
Bot__Schedules__HeadToHeadStandings__Enabled=true
Bot__Schedules__HeadToHeadStandings__Cron=0 0 17 * * ?
Bot__Notifications__Targets__0__GuildId=<FIRST-GUILD-ID>
Bot__Notifications__Targets__0__ChannelId=<FIRST-CHANNEL-ID>
Bot__Notifications__Targets__0__MentionEveryone=false
```

Keep `Bot__Discord__Token` only in the VM env file or the deployment platform's
secret provider. Never put it in `appsettings.json`, source control, logs, or a
container image.

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
