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
using UptimeMonitor.Web.Monitoring.Checking;

namespace UptimeMonitor.Tests;

// * Menyalakan aplikasi sungguhan di dalam memori untuk test (tanpa membuka port), tapi dengan HTTP palsu, jam palsu, dan database sementara
// ? "partial" dibutuhkan oleh [GeneratedRegex] di bawah; IAsyncLifetime = kait xunit untuk persiapan dan pembersihan async
public partial class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Username = "test-admin";
    public const string Password = "test-password";

    // ! Nama database memuat Guid baru, jadi tiap factory punya database sendiri dan database asli "UptimeMonitor" tidak tersentuh
    private readonly string connectionString = BuildConnectionString();

    // * Dua objek yang diatur test: balasan HTTP yang diterima checker, dan jam yang bisa dimajukan manual
    public StubHttpHandler Http { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public Task InitializeAsync() => Task.CompletedTask;

    // * Setelah semua test selesai: hapus database sementara, baru matikan aplikasi
    // ? Ditulis eksplisit (IAsyncLifetime.DisposeAsync) karena WebApplicationFactory sudah punya DisposeAsync sendiri
    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }

    // * Client yang sudah login sebagai admin; cookie login disimpan client, jadi request berikutnya dikenali sebagai admin
    // ? AllowAutoRedirect = false agar test bisa melihat respons redirect itu sendiri, bukan halaman tujuannya
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await LoginAsync(client, Username, Password);
        response.EnsureSuccessStatusCodeOrRedirect();
        return client;
    }

    // * Meniru login asli: buka /login untuk mengambil token antiforgery dari HTML, lalu kirim form beserta token itu
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

    // * Tempat mengubah isi aplikasi khusus untuk test
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ? Environment "Testing" (bukan Development), jadi appsettings.Development.json yang berisi akun admin asli tidak ikut terbaca
        builder.UseEnvironment("Testing");

        // * Konfigurasi di memori menimpa appsettings: database sementara dan akun admin khusus test
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Admin:Username"] = Username,
            ["Admin:Password"] = Password
        }));

        // * ConfigureTestServices dijalankan setelah pendaftaran asli di Program.cs, jadi bisa mengganti service yang sudah terdaftar
        builder.ConfigureTestServices(services =>
        {
            // ! Scheduler dibuang supaya tidak ada pengecekan otomatis; test menjalankan putaran sendiri agar hasilnya bisa diprediksi
            var scheduler = services.Single(descriptor => descriptor.ImplementationType == typeof(MonitorScheduler));
            services.Remove(scheduler);

            // * Jam sungguhan diganti jam palsu, dan HttpClient milik EndpointChecker diarahkan ke handler palsu
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.AddHttpClient<EndpointChecker>().ConfigurePrimaryHttpMessageHandler(() => Http);
        });
    }

    // ? Lokasi SQL Server bisa diganti lewat variabel environment UPTIMEMONITOR_TEST_SQLSERVER, bawaannya SQL Express lokal
    private static string BuildConnectionString()
    {
        var server = Environment.GetEnvironmentVariable("UPTIMEMONITOR_TEST_SQLSERVER") ?? @"localhost\SQLEXPRESS";
        return $"Server={server};Database=UptimeMonitor_Test_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";
    }

    // * Pola regex dibuat saat kompilasi (lebih cepat dari Regex biasa); mengambil isi atribut value dari input token antiforgery
    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}

public static class HttpResponseExtensions
{
    // ? Login yang berhasil membalas redirect (3xx), jadi yang dianggap gagal hanya kode di bawah 200 atau 400 ke atas
    public static void EnsureSuccessStatusCodeOrRedirect(this HttpResponseMessage response)
    {
        if ((int)response.StatusCode is < 200 or >= 400)
        {
            throw new HttpRequestException($"Unexpected status code {(int)response.StatusCode}");
        }
    }
}
