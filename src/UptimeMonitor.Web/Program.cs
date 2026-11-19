using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UptimeMonitor.Web.Api;
using UptimeMonitor.Web.Auth;
using UptimeMonitor.Web.Components;
using UptimeMonitor.Web.Data;
using UptimeMonitor.Web.Monitoring.Checking;
using UptimeMonitor.Web.Monitoring.Queries;
using UptimeMonitor.Web.Monitoring.Services;

var builder = WebApplication.CreateBuilder(args);

// * Mengaktifkan komponen Razor (.razor) dengan mode interaktif lewat server (browser berhubungan terus dengan server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// * Status login diteruskan ke semua komponen, dibutuhkan AuthorizeView dan AuthorizeRouteView
builder.Services.AddCascadingAuthenticationState();
// * ResourcesPath = folder tempat file resx bahasa berada
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// * Login berbasis cookie: setelah SignInAsync, browser membawa cookie itu di tiap request
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // * Pengguna yang belum login diarahkan ke sini
        options.LoginPath = "/login";
        // * Login berlaku 8 jam; SlidingExpiration memperpanjangnya selama pengguna masih aktif
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
// * Mengaktifkan aturan akses, misalnya RequireAuthorization() dan [Authorize]
builder.Services.AddAuthorization();

// * Membuat dokumen OpenAPI yang menggambarkan semua endpoint
builder.Services.AddOpenApi();
// * Mengaktifkan atribut validasi (seperti di MonitorInput) pada endpoint API, jadi input tidak valid otomatis dijawab 400
builder.Services.AddValidation();
// * Enum di JSON tampil sebagai teks ("Up", "Down"), bukan angka (0, 1)
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// * Mendaftarkan AppDbContext ke DI; "factory" dipilih karena komponen Blazor Server hidup lama, jadi tiap operasi membuat DbContext sendiri
// ! Blok ini harus ada sebelum "dotnet ef migrations add": tool EF mencari AppDbContext lewat Program.cs
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// * Mengisi MonitoringOptions dari section "Monitoring" di appsettings.json
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
// * Mengisi AdminOptions dari section "Admin"
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
// * Jam sungguhan untuk aplikasi; test menggantinya dengan jam palsu
builder.Services.AddSingleton(TimeProvider.System);
// * HttpClient untuk EndpointChecker dikelola factory (koneksi dipakai ulang), dan User-Agent membuat request kita dikenali server tujuan
builder.Services.AddHttpClient<EndpointChecker>(client =>
    client.DefaultRequestHeaders.UserAgent.ParseAdd("UptimeMonitor/1.0"));
// ? Scoped = satu instance per scope; scheduler membuat scope baru tiap putaran, jadi satu DbContext per putaran
builder.Services.AddScoped<MonitorCheckCycle>();
// * Layanan CRUD monitor, dipakai endpoint API dan halaman admin
builder.Services.AddScoped<MonitorService>();
// * Query baca untuk status page
builder.Services.AddScoped<StatusQueries>();
// * Mendaftarkan scheduler supaya otomatis dijalankan saat aplikasi start
builder.Services.AddHostedService<MonitorScheduler>();

var app = builder.Build();

// * Terapkan migration saat start: database dan tabelnya dibuat otomatis bila belum ada, jadi "dotnet ef database update" tidak wajib
// ? Perlu scope manual karena AppDbContext terdaftar scoped, sedangkan kode ini berjalan di luar request
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// * Di luar Development, error ditampilkan lewat halaman /Error (bukan detail teknis), dan HSTS menyuruh browser selalu memakai HTTPS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// * Alamat yang tidak ditemukan (404) ditampilkan sebagai halaman /not-found, tapi bukan untuk /api: API cukup mengembalikan kode status
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

// * Menentukan bahasa tiap request (dari cookie yang ditulis /culture/{culture}), dengan "en" sebagai default
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(SupportedCultures.Default)
    .AddSupportedCultures(SupportedCultures.All)
    .AddSupportedUICultures(SupportedCultures.All));

// ! Urutan middleware penting: Authentication dulu (siapa kamu, dari cookie), baru Authorization (boleh masuk atau tidak)
app.UseAuthentication();
app.UseAuthorization();
// * Antiforgery melindungi form dari pengiriman palsu antarsitus (CSRF); komponen Razor mewajibkannya
app.UseAntiforgery();

// * Menyajikan isi wwwroot (seperti app.css) dengan versi file dan kompresi
app.MapStaticAssets();
// * Menyajikan dokumen OpenAPI di /openapi/v1.json
// ? Tidak dibungkus IsDevelopment(), jadi tersedia di semua environment
app.MapOpenApi();
// * Mendaftarkan /api/status dan semua rute /api/monitors
app.MapMonitorApi();
// * Mendaftarkan rute /auth/login, /auth/logout, dan /culture/{culture}
app.MapAuthEndpoints();
// * Memasang halaman Razor dengan App.razor sebagai dokumen induknya
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
