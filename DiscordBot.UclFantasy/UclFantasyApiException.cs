using System.Net;

namespace DiscordBot.UclFantasy;

public sealed class UclFantasyApiException(
    UclFantasyFailureKind failureKind,
    string message,
    HttpStatusCode? statusCode = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public UclFantasyFailureKind FailureKind { get; } = failureKind;

    public HttpStatusCode? StatusCode { get; } = statusCode;
}
