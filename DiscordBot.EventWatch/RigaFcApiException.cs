using System.Net;

namespace DiscordBot.EventWatch;

public sealed class RigaFcApiException : Exception
{
    public RigaFcApiException(
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
