using System.ComponentModel.DataAnnotations;
using System.Resources;

namespace UptimeMonitor.Web.Localization;

// * Jembatan antara atribut validasi ([Required], [Range], dst.) dan file resx, supaya pesan error mengikuti bahasa
public static class ValidationMessages
{
    // ? Nama "UptimeMonitor.Web.Resources.SharedResource" = namespace + folder + nama file resx, tanpa ekstensi
    private static readonly ResourceManager Resources =
        new("UptimeMonitor.Web.Resources.SharedResource", typeof(ValidationMessages).Assembly);

    // ! Nama property harus sama persis dengan key di resx, karena dipakai lewat nameof(...)
    public static string Validation_Required => Get(nameof(Validation_Required));
    public static string Validation_Range => Get(nameof(Validation_Range));
    public static string Validation_MaxLength => Get(nameof(Validation_MaxLength));
    public static string Validation_Url => Get(nameof(Validation_Url));

    // ? Bahasa mengikuti CurrentUICulture request; key yang tidak ditemukan dikembalikan apa adanya
    private static string Get(string name) => Resources.GetString(name) ?? name;
}

// * Atribut validasi buatan sendiri: hanya menerima URL http/https
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public HttpUrlAttribute()
    {
        // * Pesan error diambil dari ValidationMessages, jadi otomatis ikut bahasa yang dipilih
        ErrorMessageResourceType = typeof(ValidationMessages);
        ErrorMessageResourceName = nameof(ValidationMessages.Validation_Url);
    }

    // ? Nilai kosong dianggap valid; urusan "wajib diisi" ditangani [Required] terpisah
    public override bool IsValid(object? value) =>
        value is null or ""
        || (value is string text
            && Uri.TryCreate(text, UriKind.Absolute, out var uri)
            // ! Cek scheme penting: "ftp://x" atau "file://x" lolos TryCreate tapi bukan URL yang boleh dicek
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
