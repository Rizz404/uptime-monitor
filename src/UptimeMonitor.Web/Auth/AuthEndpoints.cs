using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace UptimeMonitor.Web.Auth;

public static class AuthEndpoints
{
    // * Extension method ("this"): dipanggil sebagai app.MapAuthEndpoints() di Program.cs, jadi Program.cs tetap ringkas
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // * Minimal API: satu baris = satu rute HTTP + method yang menanganinya
        // ? ExcludeFromDescription menyembunyikan rute ini dari dokumen OpenAPI, karena hanya dipakai form di halaman, bukan API publik
        app.MapPost("/auth/login", Login).ExcludeFromDescription();
        // ? Cast ke Delegate menghindari ambiguitas overload MapPost untuk method yang hanya menerima HttpContext
        app.MapPost("/auth/logout", (Delegate)Logout).ExcludeFromDescription();
        app.MapGet("/culture/{culture}", SetCulture).ExcludeFromDescription();
        return app;
    }

    // * [FromForm]: nilai diambil dari field form login; bertipe nullable karena field bisa saja tidak terkirim
    // ? HttpContext dan IOptions<AdminOptions> diisi otomatis oleh framework
    private static async Task<IResult> Login(
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm] string? returnUrl,
        HttpContext context,
        IOptions<AdminOptions> admin)
    {
        var target = LocalPathOrDefault(returnUrl, "/admin");

        // * Gagal: kembali ke halaman login dengan penanda failed=true; pesannya tidak menyebut apakah username atau password yang salah
        if (!admin.Value.Matches(username ?? string.Empty, password ?? string.Empty))
        {
            return Results.LocalRedirect($"/login?failed=true&returnUrl={Uri.EscapeDataString(target)}");
        }

        // * "Claim" = fakta tentang pengguna (di sini cuma nama); identity dibuat dengan skema cookie yang didaftarkan di Program.cs
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, admin.Value.Username)],
            CookieAuthenticationDefaults.AuthenticationScheme);

        // * SignInAsync menulis cookie login ke browser, dan request berikutnya dikenali sebagai admin
        await context.SignInAsync(new ClaimsPrincipal(identity));
        // ? LocalRedirect hanya menerima alamat di situs ini, lapisan pengaman kedua setelah LocalPathOrDefault
        return Results.LocalRedirect(target);
    }

    // * SignOutAsync menghapus cookie login
    private static async Task<IResult> Logout(HttpContext context)
    {
        await context.SignOutAsync();
        return Results.LocalRedirect("/");
    }

    // * Pilihan bahasa disimpan di cookie selama 1 tahun, lalu dibaca middleware localization di Program.cs
    private static IResult SetCulture(string culture, string? returnUrl, HttpContext context)
    {
        // ! Hanya bahasa yang terdaftar yang diterima; nilai sembarang dari URL diabaikan
        if (SupportedCultures.All.Contains(culture))
        {
            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                // ? IsEssential = cookie tetap ditulis walau pengguna belum menyetujui cookie
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
        }

        return Results.LocalRedirect(LocalPathOrDefault(returnUrl, "/"));
    }

    // ! Mencegah open redirect: hanya path yang diawali satu "/" yang diterima, sedangkan "//evil.com" dan "/\evil.com" dianggap browser sebagai situs lain
    // ? path is ['/', ..] = pola list: karakter pertama harus "/"
    private static string LocalPathOrDefault(string? path, string fallback) =>
        path is ['/', ..] && !path.StartsWith("//") && !path.StartsWith("/\\") ? path : fallback;
}

// * Daftar bahasa dipakai bersama oleh endpoint di atas dan middleware localization di Program.cs
public static class SupportedCultures
{
    public const string Default = "en";
    public static readonly string[] All = ["en", "id"];
}
