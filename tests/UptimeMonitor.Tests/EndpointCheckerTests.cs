using System.Net;
using UptimeMonitor.Web.Monitoring.Checking;
using UptimeMonitor.Web.Monitoring.Entities;

namespace UptimeMonitor.Tests;

public class EndpointCheckerTests
{
    // ? xunit membuat instance class baru untuk SETIAP test, jadi handler dan monitor ini selalu bersih
    private readonly StubHttpHandler handler = new();
    private readonly EndpointMonitor monitor = new() { Name = "Example", Url = "https://example.test/" };

    private EndpointChecker CreateChecker() => new(new HttpClient(handler));

    // * Pola tiap test: siapkan (handler), jalankan (CheckAsync), lalu periksa (Assert)
    [Fact]
    public async Task Expected_status_code_is_reported_as_up()
    {
        handler.RespondWith(HttpStatusCode.OK);

        var outcome = await CreateChecker().CheckAsync(monitor, CancellationToken.None);

        Assert.True(outcome.IsUp);
        Assert.Equal(200, outcome.StatusCode);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public async Task Unexpected_status_code_is_reported_as_down()
    {
        handler.RespondWith(HttpStatusCode.ServiceUnavailable);

        var outcome = await CreateChecker().CheckAsync(monitor, CancellationToken.None);

        Assert.False(outcome.IsUp);
        Assert.Equal(503, outcome.StatusCode);
        Assert.Equal("Expected status 200 but received 503", outcome.Error);
    }

    // * Membuktikan "Up" mengikuti ExpectedStatusCode, bukan angka 200 yang dikunci
    [Fact]
    public async Task Custom_expected_status_code_is_respected()
    {
        monitor.ExpectedStatusCode = 204;
        handler.RespondWith(HttpStatusCode.NoContent);

        var outcome = await CreateChecker().CheckAsync(monitor, CancellationToken.None);

        Assert.True(outcome.IsUp);
    }

    [Fact]
    public async Task Slow_endpoint_is_reported_as_timed_out()
    {
        monitor.TimeoutSeconds = 1;
        // ? Delay tanpa batas yang hanya berhenti saat dibatalkan, jadi yang memutus adalah timeout 1 detik milik checker
        handler.Respond = async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var outcome = await CreateChecker().CheckAsync(monitor, CancellationToken.None);

        Assert.False(outcome.IsUp);
        Assert.Null(outcome.StatusCode);
        Assert.Equal("Timed out after 1 seconds", outcome.Error);
    }

    // * Handler melempar HttpRequestException untuk meniru jaringan putus
    [Fact]
    public async Task Connection_failure_is_reported_as_down()
    {
        handler.Respond = (_, _) => throw new HttpRequestException("No such host is known");

        var outcome = await CreateChecker().CheckAsync(monitor, CancellationToken.None);

        Assert.False(outcome.IsUp);
        Assert.Equal("No such host is known", outcome.Error);
    }
}
