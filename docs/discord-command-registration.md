# Discord application-command registration

The production bot registers its desired slash commands globally. Global
commands are appropriate because the bot exposes the same command set in every
guild and should not create a separate copy for each guild.

Registration runs once after the Discord gateway first becomes ready during a
process startup. It uses Discord's bulk-overwrite endpoint, so removed or
renamed commands are reconciled intentionally from the command definitions in
`DiscordApplicationCommands`. Later gateway reconnects do not register again.
If registration fails, the bot logs the scope, HTTP and Discord error details,
and validation errors, and waits for a process restart before another attempt.

Production uses this configuration:

```json
{
  "Discord": {
    "Commands": {
      "RegistrationMode": "Global"
    }
  }
}
```

Development defaults to `Disabled` so starting a local process cannot mutate
production commands accidentally. To test a command with Discord's faster
guild-scoped propagation, provide these environment variables for a development
bot application and test guild:

```dotenv
Discord__Commands__RegistrationMode=Guild
Discord__Commands__GuildId=<TEST-GUILD-ID>
```

Use a separate development bot token as `BOT_TOKEN`; do not point guild-scoped
development registration at the production application. `RegistrationMode`
also accepts `Disabled` when a process should handle commands without changing
their registration.
