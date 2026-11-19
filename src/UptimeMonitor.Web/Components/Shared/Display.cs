namespace UptimeMonitor.Web.Components.Shared;

// * Helper format tampilan yang dipakai beberapa halaman, supaya aturan penulisannya sama di mana-mana
public static class Display
{
    // ? Waktu selalu ditampilkan dalam UTC dan diberi label, agar tidak membingungkan antar zona waktu
    public static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("dd MMM yyyy HH:mm") + " UTC";

    // * Tanda "—" untuk data kosong, selaras dengan Uptime.Percentage yang mengembalikan null bila belum ada data
    public static string Percentage(double? value) => value is null ? "—" : value.Value.ToString("0.00") + "%";

    // * Memilih class CSS warna menurut persentase: 99% ke atas baik, 95% ke atas waspada, di bawahnya buruk
    public static string UptimeClass(double? value) => value switch
    {
        null => "uptime-none",
        >= 99 => "uptime-good",
        >= 95 => "uptime-warn",
        _ => "uptime-bad"
    };
}
