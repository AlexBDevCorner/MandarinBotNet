using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DiscordBot;

public interface IAutonomousWorkDispatcherClient
{
    Task TriggerDispatcherAsync(CancellationToken cancellationToken);
}

public sealed class AutonomousWorkDispatcherException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public AutonomousWorkDispatcherException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

public sealed class AutonomousWorkDispatcherClient(
    HttpClient httpClient,
    AutonomousWorkDispatcherOptions options,
    ILogger<AutonomousWorkDispatcherClient> logger) : IAutonomousWorkDispatcherClient
{
    public const string GitHubApiVersion = "2022-11-28";
    public const string GitHubJsonMediaType = "application/vnd.github+json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task TriggerDispatcherAsync(CancellationToken cancellationToken)
    {
        var owner = options.Owner;
        var repository = options.Repository;
        var workflow = options.Workflow;
        var gitRef = options.Ref;

        if (string.IsNullOrWhiteSpace(owner) ||
            string.IsNullOrWhiteSpace(repository) ||
            string.IsNullOrWhiteSpace(workflow) ||
            string.IsNullOrWhiteSpace(gitRef))
        {
            throw new AutonomousWorkDispatcherException(
                $"AutonomousWork dispatcher configuration is incomplete for '{owner}/{repository}' workflow '{workflow}'.");
        }

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw new AutonomousWorkDispatcherException(
                $"AutonomousWork dispatcher token is missing for '{owner}/{repository}' workflow '{workflow}'.");
        }

        var endpoint = $"repos/{owner}/{repository}/actions/workflows/{workflow}/dispatches";
        var payload = JsonSerializer.Serialize(
            new Dictionary<string, string> { ["ref"] = gitRef },
            SerializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GitHubJsonMediaType));
        request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "Failed to trigger AutonomousWork dispatcher for {Owner}/{Repository} workflow {Workflow} ref {Ref}: transport failure.",
                owner,
                repository,
                workflow,
                gitRef);
            throw new AutonomousWorkDispatcherException(
                $"Failed to trigger AutonomousWork dispatcher for '{owner}/{repository}' workflow '{workflow}' ref '{gitRef}': transport failure.",
                innerException: exception);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Triggered AutonomousWork dispatcher for {Owner}/{Repository} workflow {Workflow} ref {Ref} with status {StatusCode}.",
                    owner,
                    repository,
                    workflow,
                    gitRef,
                    (int)response.StatusCode);
                return;
            }

            var body = await ReadBodySafeAsync(response, cancellationToken);
            logger.LogError(
                "Failed to trigger AutonomousWork dispatcher for {Owner}/{Repository} workflow {Workflow} ref {Ref}: GitHub returned {StatusCode} with body {Body}.",
                owner,
                repository,
                workflow,
                gitRef,
                (int)response.StatusCode,
                Truncate(body, 2000));

            throw new AutonomousWorkDispatcherException(
                $"Failed to trigger AutonomousWork dispatcher for '{owner}/{repository}' workflow '{workflow}' ref '{gitRef}': GitHub returned {(int)response.StatusCode}. Body: {Truncate(body, 2000)}",
                response.StatusCode);
        }
    }

    private static async Task<string> ReadBodySafeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
