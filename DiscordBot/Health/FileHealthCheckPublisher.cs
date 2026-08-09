using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DiscordBot.Health;

public sealed class FileHealthCheckPublisher(
    string healthStatePath,
    TimeProvider timeProvider) : IHealthCheckPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _healthStatePath = Path.GetFullPath(
        string.IsNullOrWhiteSpace(healthStatePath)
            ? throw new ArgumentException("A health state path is required.", nameof(healthStatePath))
            : healthStatePath);

    public async Task PublishAsync(
        HealthReport report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        var state = new PublishedHealthState(
            SchemaVersion: 1,
            ObservedAtUtc: timeProvider.GetUtcNow(),
            Live: true,
            Ready: report.Status == HealthStatus.Healthy,
            Checks: report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status.ToString(),
                StringComparer.Ordinal));

        var directory = Path.GetDirectoryName(_healthStatePath)
            ?? throw new InvalidOperationException("The health state path must include a directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_healthStatePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    state,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _healthStatePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}

public sealed record PublishedHealthState(
    int SchemaVersion,
    DateTimeOffset ObservedAtUtc,
    bool Live,
    bool Ready,
    IReadOnlyDictionary<string, string> Checks);
