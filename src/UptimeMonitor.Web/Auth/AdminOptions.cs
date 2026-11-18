using System.Security.Cryptography;
using System.Text;

namespace UptimeMonitor.Web.Auth;

// * Satu akun admin, dibaca dari section "Admin" di konfigurasi (pola sama dengan MonitoringOptions)
public class AdminOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // ! Bila username/password belum diisi di konfigurasi, tidak ada yang bisa login (aman secara default)
    // ! Operator "&" (bukan "&&") disengaja: kedua pembandingan selalu dijalankan, jadi waktu respons tidak membocorkan mana yang salah
    public bool Matches(string username, string password) =>
        Username.Length > 0
        && Password.Length > 0
        && FixedTimeEquals(username, Username) & FixedTimeEquals(password, Password);

    // * Pembandingan waktu-konstan: "==" berhenti di huruf pertama yang beda, sehingga lama prosesnya bisa dipakai menebak password
    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
