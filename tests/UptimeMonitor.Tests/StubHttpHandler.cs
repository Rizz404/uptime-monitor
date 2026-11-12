using System.Net;

namespace UptimeMonitor.Tests;

public class StubHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public void RespondWith(HttpStatusCode statusCode) =>
        Respond = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Respond(request, cancellationToken);
}
