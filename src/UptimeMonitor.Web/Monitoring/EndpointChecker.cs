using System.Diagnostics;

namespace UptimeMonitor.Web.Monitoring;

public class EndpointChecker(HttpClient http)
{
    public async Task<CheckOutcome> CheckAsync(EndpointMonitor monitor, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(monitor.TimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await http.GetAsync(monitor.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var statusCode = (int)response.StatusCode;
            var isUp = statusCode == monitor.ExpectedStatusCode;
            var error = isUp ? null : $"Expected status {monitor.ExpectedStatusCode} but received {statusCode}";
            return new CheckOutcome(isUp, statusCode, Elapsed(stopwatch), error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CheckOutcome(false, null, Elapsed(stopwatch), $"Timed out after {monitor.TimeoutSeconds} seconds");
        }
        catch (HttpRequestException exception)
        {
            return new CheckOutcome(false, null, Elapsed(stopwatch), exception.Message);
        }
    }

    private static int Elapsed(Stopwatch stopwatch) => (int)stopwatch.ElapsedMilliseconds;
}
