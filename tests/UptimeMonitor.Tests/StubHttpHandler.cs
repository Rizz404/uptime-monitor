using System.Net;

namespace UptimeMonitor.Tests;

// * HTTP palsu: HttpClient memanggil handler ini, bukan internet sungguhan, jadi test cepat dan hasilnya bisa diatur
public class StubHttpHandler : HttpMessageHandler
{
    // * Perilaku bisa diganti per test; nilai awalnya selalu membalas 200 OK. "_" = parameter yang tidak dipakai
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    // ? Jalan pintas untuk kasus paling umum: cukup tentukan status code-nya
    public void RespondWith(HttpStatusCode statusCode) =>
        Respond = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode));

    // * Method yang dipanggil HttpClient tiap ada request, diteruskan ke Respond
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Respond(request, cancellationToken);
}
