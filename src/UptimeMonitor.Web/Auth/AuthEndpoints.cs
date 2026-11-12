using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace UptimeMonitor.Web.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", Login).ExcludeFromDescription();
        app.MapPost("/auth/logout", (Delegate)Logout).ExcludeFromDescription();
        app.MapGet("/culture/{culture}", SetCulture).ExcludeFromDescription();
        return app;
    }

    private static async Task<IResult> Login(
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm] string? returnUrl,
        HttpContext context,
        IOptions<AdminOptions> admin)
    {
        var target = LocalPathOrDefault(returnUrl, "/admin");

        if (!admin.Value.Matches(username ?? string.Empty, password ?? string.Empty))
        {
            return Results.LocalRedirect($"/login?failed=true&returnUrl={Uri.EscapeDataString(target)}");
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, admin.Value.Username)],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await context.SignInAsync(new ClaimsPrincipal(identity));
        return Results.LocalRedirect(target);
    }

    private static async Task<IResult> Logout(HttpContext context)
    {
        await context.SignOutAsync();
        return Results.LocalRedirect("/");
    }

    private static IResult SetCulture(string culture, string? returnUrl, HttpContext context)
    {
        if (SupportedCultures.All.Contains(culture))
        {
            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
        }

        return Results.LocalRedirect(LocalPathOrDefault(returnUrl, "/"));
    }

    private static string LocalPathOrDefault(string? path, string fallback) =>
        path is ['/', ..] && !path.StartsWith("//") && !path.StartsWith("/\\") ? path : fallback;
}

public static class SupportedCultures
{
    public const string Default = "en";
    public static readonly string[] All = ["en", "id"];
}
