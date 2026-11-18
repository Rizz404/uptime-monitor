using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UptimeMonitor.Web.Data;
using UptimeMonitor.Web.Monitoring;

namespace UptimeMonitor.Tests;

// * Menguji aplikasi secara utuh (API, login, database, siklus pengecekan) lewat AppFactory
// ! IClassFixture: satu aplikasi dan satu database dipakai bersama semua test di class ini, jadi tiap test memakai nama monitor sendiri agar tidak saling mengganggu
public class IntegrationTests(AppFactory factory) : IClassFixture<AppFactory>
{
    // * Skenario lengkap: gagal 3 kali membuka insiden, lalu satu sukses menutupnya
    [Fact]
    public async Task Check_cycle_opens_incident_after_threshold_and_resolves_it_on_recovery()
    {
        var monitor = await CreateMonitorAsync("Cycle incident");

        factory.Http.RespondWith(HttpStatusCode.InternalServerError);
        await RunCycleAsync();
        await RunCycleAsync();

        // * Dua gagal, belum mencapai batas 3, jadi belum ada insiden
        Assert.Empty(await IncidentsAsync(monitor.Id));

        await RunCycleAsync();

        // * Gagal ketiga: insiden dibuka dengan penyebab dari pesan error checker
        var opened = Assert.Single(await IncidentsAsync(monitor.Id));
        Assert.Null(opened.ResolvedAt);
        Assert.Equal("Expected status 200 but received 500", opened.Cause);

        // * Endpoint "pulih": satu sukses menutup insiden yang sama (bukan membuat yang baru)
        factory.Http.RespondWith(HttpStatusCode.OK);
        await RunCycleAsync();

        var resolved = Assert.Single(await IncidentsAsync(monitor.Id));
        Assert.NotNull(resolved.ResolvedAt);

        var stored = await FindMonitorAsync(monitor.Id);
        Assert.Equal(MonitorStatus.Up, stored.Status);
        Assert.False(stored.HasOpenIncident);
        Assert.Equal(4, await CheckCountAsync(monitor.Id));
    }

    // * advance: false = jam tidak dimajukan, jadi putaran kedua melihat monitor aktif belum jatuh tempo, dan monitor yang dijeda memang tidak pernah dicek
    [Fact]
    public async Task Check_cycle_skips_monitors_that_are_paused_or_not_yet_due()
    {
        factory.Http.RespondWith(HttpStatusCode.OK);
        var active = await CreateMonitorAsync("Cycle active");
        var paused = await CreateMonitorAsync("Cycle paused");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MonitorService>().SetPausedAsync(paused.Id, true);
        }

        await RunCycleAsync(advance: false);
        await RunCycleAsync(advance: false);

        Assert.Equal(1, await CheckCountAsync(active.Id));
        Assert.Equal(0, await CheckCountAsync(paused.Id));
    }

    // * Client tanpa login: API monitor harus menjawab 401
    [Fact]
    public async Task Monitor_api_rejects_anonymous_requests()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/monitors");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // * Password salah: dialihkan balik ke login dan tetap tidak punya akses ke API
    [Fact]
    public async Task Login_with_wrong_password_redirects_back_to_login()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await AppFactory.LoginAsync(client, AppFactory.Username, "wrong-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login?failed=true", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/monitors")).StatusCode);
    }

    // * Tiga field sengaja dibuat salah (nama kosong, URL ftp, interval di bawah 30); respons 400 harus menyebut ketiganya
    [Fact]
    public async Task Monitor_api_validates_input()
    {
        var client = await factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/monitors", new MonitorInput
        {
            Name = string.Empty,
            Url = "ftp://example.test/",
            IntervalSeconds = 5
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Name", body);
        Assert.Contains("Url", body);
        Assert.Contains("IntervalSeconds", body);
    }

    // * Admin membuat monitor, lalu client anonim melihatnya di /api/status; status "Pending" tampil sebagai teks berkat converter enum
    [Fact]
    public async Task Created_monitor_appears_on_public_status_endpoint()
    {
        var admin = await factory.CreateAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api/monitors", new MonitorInput
        {
            Name = "Public status",
            Url = "https://public-status.test/"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var anonymous = factory.CreateClient();
        var body = await anonymous.GetStringAsync("/api/status");

        Assert.Contains("\"name\":\"Public status\"", body);
        Assert.Contains("\"status\":\"Pending\"", body);
    }

    // * Jeda, ubah, dan hapus lewat API; hasilnya diperiksa lewat database (FindMonitorAsync), bukan hanya dari kode respons
    [Fact]
    public async Task Admin_can_pause_update_and_delete_a_monitor()
    {
        var client = await factory.CreateAdminClientAsync();
        var monitor = await CreateMonitorAsync("Lifecycle");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/monitors/{monitor.Id}/pause", null)).StatusCode);
        Assert.True((await FindMonitorAsync(monitor.Id)).IsPaused);

        var update = await client.PutAsJsonAsync($"/api/monitors/{monitor.Id}", new MonitorInput
        {
            Name = "Lifecycle renamed",
            Url = "https://lifecycle.test/health",
            IntervalSeconds = 120
        });

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal("Lifecycle renamed", (await FindMonitorAsync(monitor.Id)).Name);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/monitors/{monitor.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/monitors/{monitor.Id}")).StatusCode);
    }

    // ? Header Accept-Language dibaca middleware localization bila tidak ada cookie bahasa, jadi "id" menghasilkan teks Indonesia
    [Fact]
    public async Task Status_page_is_rendered_in_the_requested_language()
    {
        var client = factory.CreateClient();

        var english = await client.GetStringAsync("/");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("id");
        var indonesian = await client.GetStringAsync("/");

        Assert.Contains("System status", english);
        Assert.Contains("Status sistem", indonesian);
    }

    // * Halaman /admin tanpa login dialihkan ke /login
    [Fact]
    public async Task Dashboard_redirects_anonymous_visitors_to_login()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString);
    }

    // * Helper di bawah memakai scope baru tiap kali (service bersifat scoped) dan menyentuh database langsung
    // ? Interval 30 detik sama dengan lompatan jam di RunCycleAsync, jadi monitor ini tepat jatuh tempo lagi di putaran berikutnya
    private async Task<EndpointMonitor> CreateMonitorAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MonitorService>().CreateAsync(new MonitorInput
        {
            Name = name,
            Url = "https://example.test/",
            IntervalSeconds = 30
        });
    }

    // * Menjalankan satu putaran seperti scheduler, lalu (bila advance) memajukan jam palsu 30 detik
    private async Task RunCycleAsync(bool advance = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MonitorCheckCycle>().RunAsync(CancellationToken.None);

        if (advance)
        {
            factory.Time.Advance(TimeSpan.FromSeconds(30));
        }
    }

    private async Task<EndpointMonitor> FindMonitorAsync(int id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Monitors.SingleAsync(monitor => monitor.Id == id);
    }

    private async Task<List<Incident>> IncidentsAsync(int id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Incidents
            .Where(incident => incident.MonitorId == id)
            .ToListAsync();
    }

    private async Task<int> CheckCountAsync(int id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().CheckResults.CountAsync(result => result.MonitorId == id);
    }
}
