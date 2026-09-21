namespace DiscordBot;

public sealed class AutonomousWorkDispatcherOptions
{
    public const string DefaultCron = "0 0/10 * * * ?";
    public const string DefaultOwner = "AlexBDevCorner";
    public const string DefaultRepository = "AutonomousWork";
    public const string DefaultWorkflow = "dispatch.yml";
    public const string DefaultRef = "master";

    public bool Enabled { get; init; }

    public string Cron { get; init; } = DefaultCron;

    public string Owner { get; init; } = DefaultOwner;

    public string Repository { get; init; } = DefaultRepository;

    public string Workflow { get; init; } = DefaultWorkflow;

    public string Ref { get; init; } = DefaultRef;

    public string Token { get; init; } = string.Empty;
}
