namespace UptimeMonitor.Web.Monitoring.Queries;

// * Helper murni (tanpa database) untuk menghitung persentase uptime, jadi mudah diuji
public static class Uptime
{
    // ! 0 pengecekan menghasilkan null (bukan 0%), karena "belum ada data" berbeda dari "selalu down"
    // ! Dikali 100.0 (bukan 100) agar pembagian memakai pecahan, kalau semuanya int hasilnya dibulatkan ke bawah
    public static double? Percentage(int successful, int total) =>
        total == 0 ? null : Math.Round(successful * 100.0 / total, 2);
}
