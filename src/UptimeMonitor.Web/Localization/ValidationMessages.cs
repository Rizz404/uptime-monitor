using System.ComponentModel.DataAnnotations;
using System.Resources;

namespace UptimeMonitor.Web.Localization;

public static class ValidationMessages
{
    private static readonly ResourceManager Resources =
        new("UptimeMonitor.Web.Resources.SharedResource", typeof(ValidationMessages).Assembly);

    public static string Validation_Required => Get(nameof(Validation_Required));
    public static string Validation_Range => Get(nameof(Validation_Range));
    public static string Validation_MaxLength => Get(nameof(Validation_MaxLength));
    public static string Validation_Url => Get(nameof(Validation_Url));

    private static string Get(string name) => Resources.GetString(name) ?? name;
}

public sealed class HttpUrlAttribute : ValidationAttribute
{
    public HttpUrlAttribute()
    {
        ErrorMessageResourceType = typeof(ValidationMessages);
        ErrorMessageResourceName = nameof(ValidationMessages.Validation_Url);
    }

    public override bool IsValid(object? value) =>
        value is null or ""
        || (value is string text
            && Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
