using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using UptimeMonitor.Web.Data;
using UptimeMonitor.Web.Monitoring;

namespace UptimeMonitor.Tests;

public partial class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Username = "test-admin";
    public const string Password = "test-password";

    private readonly string connectionString = BuildConnectionString();

    public StubHttpHandler Http { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }

    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await LoginAsync(client, Username, Password);
        response.EnsureSuccessStatusCodeOrRedirect();
        return client;
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password)
    {
        var page = await client.GetStringAsync("/login");
        var token = AntiforgeryToken().Match(page).Groups[1].Value;

        return await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["username"] = username,
            ["password"] = password
        }));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Admin:Username"] = Username,
            ["Admin:Password"] = Password
        }));

        builder.ConfigureTestServices(services =>
        {
            var scheduler = services.Single(descriptor => descriptor.ImplementationType == typeof(MonitorScheduler));
            services.Remove(scheduler);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.AddHttpClient<EndpointChecker>().ConfigurePrimaryHttpMessageHandler(() => Http);
        });
    }

    private static string BuildConnectionString()
    {
        var server = Environment.GetEnvironmentVariable("UPTIMEMONITOR_TEST_SQLSERVER") ?? @"localhost\SQLEXPRESS";
        return $"Server={server};Database=UptimeMonitor_Test_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}

public static class HttpResponseExtensions
{
    public static void EnsureSuccessStatusCodeOrRedirect(this HttpResponseMessage response)
    {
        if ((int)response.StatusCode is < 200 or >= 400)
        {
            throw new HttpRequestException($"Unexpected status code {(int)response.StatusCode}");
        }
    }
}
