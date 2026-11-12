namespace UptimeMonitor.Web.Components.Shared;

public static class Display
{
    public static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("dd MMM yyyy HH:mm") + " UTC";

    public static string Percentage(double? value) => value is null ? "—" : value.Value.ToString("0.00") + "%";

    public static string UptimeClass(double? value) => value switch
    {
        null => "uptime-none",
        >= 99 => "uptime-good",
        >= 95 => "uptime-warn",
        _ => "uptime-bad"
    };
}
