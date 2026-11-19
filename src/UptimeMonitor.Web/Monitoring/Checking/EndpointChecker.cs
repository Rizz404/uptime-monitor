using System.Diagnostics;
using UptimeMonitor.Web.Monitoring.Entities;

namespace UptimeMonitor.Web.Monitoring.Checking;

// * Tugasnya satu: mengecek satu URL lewat HTTP GET lalu mengembalikan CheckOutcome. Tidak tahu soal database
// ? HttpClient disuntik dari luar (bukan dibuat di sini), jadi di test bisa diganti HTTP palsu
public class EndpointChecker(HttpClient http)
{
    // * async/await: thread tidak diam menunggu respons, dan CancellationToken dari pemanggil dipakai untuk membatalkan (misalnya saat aplikasi dimatikan)
    public async Task<CheckOutcome> CheckAsync(EndpointMonitor monitor, CancellationToken cancellationToken)
    {
        // * Token gabungan: batal bila pemanggil membatalkan ATAU batas TimeoutSeconds terlewati
        // ? "using var" = objek dibuang otomatis di akhir method
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(monitor.TimeoutSeconds));
        // * Stopwatch mulai dari sini, jadi latency = waktu sampai respons (atau gagal) diterima
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // * ResponseHeadersRead: berhenti setelah header diterima, isi halaman tidak ikut diunduh karena hanya status code yang dibutuhkan
            using var response = await http.GetAsync(monitor.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var statusCode = (int)response.StatusCode;
            // ! Up berarti status code SAMA dengan yang diharapkan, bukan sekadar 2xx
            var isUp = statusCode == monitor.ExpectedStatusCode;
            var error = isUp ? null : $"Expected status {monitor.ExpectedStatusCode} but received {statusCode}";
            return new CheckOutcome(isUp, statusCode, Elapsed(stopwatch), error);
        }
        // ! "when (...)" membedakan timeout dari pembatalan pemanggil: bila pemanggil yang membatalkan, exception sengaja dilempar terus
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CheckOutcome(false, null, Elapsed(stopwatch), $"Timed out after {monitor.TimeoutSeconds} seconds");
        }
        // * Gagal konek (DNS, koneksi ditolak, dst.) dianggap down, bukan crash
        catch (HttpRequestException exception)
        {
            return new CheckOutcome(false, null, Elapsed(stopwatch), exception.Message);
        }
    }

    private static int Elapsed(Stopwatch stopwatch) => (int)stopwatch.ElapsedMilliseconds;
}
