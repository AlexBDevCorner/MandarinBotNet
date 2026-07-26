using System.Net;

namespace DiscordBot.FantasyPremierLeague;

public sealed class FantasyPremierLeagueApiException : Exception
{
    public FantasyPremierLeagueApiException(
        FantasyPremierLeagueFailureKind failureKind,
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        StatusCode = statusCode;
    }

    public FantasyPremierLeagueFailureKind FailureKind { get; }

    public HttpStatusCode? StatusCode { get; }
}
