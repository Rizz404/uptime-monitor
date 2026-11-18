using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UptimeMonitor.Web.Api;
using UptimeMonitor.Web.Auth;
using UptimeMonitor.Web.Components;
using UptimeMonitor.Web.Data;
using UptimeMonitor.Web.Monitoring;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// * Mendaftarkan AppDbContext ke DI; "factory" dipilih karena komponen Blazor Server hidup lama, jadi tiap operasi membuat DbContext sendiri
// ! Blok ini harus ada sebelum "dotnet ef migrations add": tool EF mencari AppDbContext lewat Program.cs
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<EndpointChecker>(client =>
    client.DefaultRequestHeaders.UserAgent.ParseAdd("UptimeMonitor/1.0"));
builder.Services.AddScoped<MonitorCheckCycle>();
builder.Services.AddScoped<MonitorService>();
builder.Services.AddScoped<StatusQueries>();
builder.Services.AddHostedService<MonitorScheduler>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(SupportedCultures.Default)
    .AddSupportedCultures(SupportedCultures.All)
    .AddSupportedUICultures(SupportedCultures.All));

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapOpenApi();
app.MapMonitorApi();
app.MapAuthEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
