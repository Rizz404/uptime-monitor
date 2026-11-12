using System.Security.Cryptography;
using System.Text;

namespace UptimeMonitor.Web.Auth;

public class AdminOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool Matches(string username, string password) =>
        Username.Length > 0
        && Password.Length > 0
        && FixedTimeEquals(username, Username) & FixedTimeEquals(password, Password);

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
