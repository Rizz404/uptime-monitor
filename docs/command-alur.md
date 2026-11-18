# Command sepanjang alur pembuatan Uptime Monitor

Command apa saja yang dijalankan per tahap, dan apa hasilnya.

Tanda di kolom pertama:

- **✔** ada buktinya di repo (isi file, header, atau kode yang memanggilnya)
- **~** rekonstruksi: bisa berbeda dari yang dulu diketik, tapi hasilnya setara. Belum diuji.

---

## Tahap 1: Bootstrap (`c44b80f`)

Commit ini hanya berisi 6 file konfigurasi, belum ada kode C#.

|     | Command                                                                                  | Menghasilkan                                                                                                                    |
| --- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| ✔   | `dotnet new gitignore`                                                                   | [.gitignore](../.gitignore). Header file itu sendiri menyebut perintah ini                                                      |
| ~   | `dotnet new sln --format slnx -n UptimeMonitor`                                          | [UptimeMonitor.slnx](../UptimeMonitor.slnx), file solution kosong                                                               |
| ~   | `dotnet new blazor -n UptimeMonitor.Web -o src/UptimeMonitor.Web --interactivity Server` | Project Web: `.csproj`, `Program.cs`, folder `Components/`, `wwwroot/`, `Properties/launchSettings.json`                        |
| ~   | `dotnet new xunit -n UptimeMonitor.Tests -o tests/UptimeMonitor.Tests`                   | Project test: `.csproj` dan contoh `UnitTest1.cs` (nanti dihapus)                                                               |
| ~   | `dotnet sln add src/UptimeMonitor.Web tests/UptimeMonitor.Tests`                         | Dua baris `<Project>` di dalam `.slnx`                                                                                          |
| ~   | `dotnet add tests/UptimeMonitor.Tests reference src/UptimeMonitor.Web`                   | Baris `<ProjectReference>` di csproj test                                                                                       |
| ~   | `dotnet new tool-manifest`                                                               | File manifest tool. Defaultnya di `.config/dotnet-tools.json`; di repo ini ada di root. Keduanya dikenali `dotnet tool restore` |
| ✔   | `dotnet tool install dotnet-ef --version 10.0.15`                                        | Entry `dotnet-ef` di [dotnet-tools.json](../dotnet-tools.json)                                                                  |
| ~   | `dotnet add src/UptimeMonitor.Web package <nama> --version 10.0.15`                      | Baris `<PackageReference>` di csproj, untuk `EntityFrameworkCore.SqlServer`, `EntityFrameworkCore.Design`, `AspNetCore.OpenApi` |
| ~   | `dotnet add tests/UptimeMonitor.Tests package <nama>`                                    | Paket test: `Mvc.Testing`, `TimeProvider.Testing`                                                                               |
| ✔   | `dotnet build`                                                                           | Folder `bin/` dan `obj/` (di-ignore git), pesan "Build succeeded"                                                               |

Paket NuGet dipin ke 10.0.x karena versi terbaru di NuGet sudah 11 dan tidak cocok dengan .NET 10.

---

## Tahap 2 dan 3: Localization dan Monitoring

Hampir tidak ada command khusus; kamu hanya menulis file. Yang dipakai berulang:

|     | Command                                        | Menghasilkan                                                                                              |
| --- | ---------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| ✔   | `dotnet build`                                 | Mengecek kompilasi. File `.resx` otomatis dikompilasi jadi resource (class `SharedResource` dipakai kode) |
| ~   | `dotnet watch --project src/UptimeMonitor.Web` | Build ulang otomatis saat file berubah (opsional)                                                         |

---

## Tahap 4: Database

Satu-satunya tahap yang kaya command.

|     | Command                                                                                               | Menghasilkan                                                                                                                                                                                       |
| --- | ----------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ✔   | `dotnet tool restore`                                                                                 | Mengunduh tool sesuai `dotnet-tools.json`, supaya `dotnet ef` bisa dipakai                                                                                                                         |
| ✔   | `dotnet ef migrations add InitialCreate --project src/UptimeMonitor.Web --output-dir Data/Migrations` | **3 file** di `Data/Migrations/`: `20261112065410_InitialCreate.cs` (perintah buat tabel), `.Designer.cs`, dan `AppDbContextModelSnapshot.cs`. Angka di depan nama file adalah timestamp pembuatan |
| ~   | `dotnet ef migrations list --project src/UptimeMonitor.Web`                                           | Daftar migration, dan mana yang sudah diterapkan ke database                                                                                                                                       |
| ~   | `dotnet ef database update --project src/UptimeMonitor.Web`                                           | Membuat database `UptimeMonitor` di SQL Server Express beserta tabelnya, plus tabel `__EFMigrationsHistory`                                                                                        |

Dua hal yang sering membingungkan:

- `migrations add` hanya **membuat file**. Database baru berubah saat migration **diterapkan**. Itu dua langkah berbeda.
- `dotnet ef database update` **tidak wajib**. [Program.cs:47](../src/UptimeMonitor.Web/Program.cs#L47) sudah memanggil `MigrateAsync()`, jadi database dibuat otomatis saat aplikasi pertama kali dijalankan.

---

## Tahap 5 sampai 7: Auth, API, UI

Tidak ada command baru selain `dotnet build`. Mulai di sini aplikasinya dijalankan:

|     | Command                                                            | Menghasilkan                                                                                                      |
| --- | ------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------- |
| ✔   | `dotnet run --project src/UptimeMonitor.Web --launch-profile http` | Aplikasi jalan di `http://localhost:5083`, migration otomatis diterapkan, browser terbuka (`launchBrowser: true`) |
| ✔   | buka `http://localhost:5083/openapi/v1.json`                       | Dokumen OpenAPI dari `app.MapOpenApi()`. Hanya ada di Development                                                 |

Profile `https` juga tersedia (`https://localhost:7058`), lihat [launchSettings.json](../src/UptimeMonitor.Web/Properties/launchSettings.json).

---

## Tahap 8: Test

|     | Command                                                     | Menghasilkan                                                 |
| --- | ----------------------------------------------------------- | ------------------------------------------------------------ |
| ✔   | `dotnet test`                                               | Ringkasan Passed/Failed. Butuh SQL Server Express yang aktif |
| ~   | `dotnet test --filter "FullyQualifiedName~EndpointChecker"` | Hanya test yang namanya cocok                                |
| ~   | `dotnet test --logger "console;verbosity=detailed"`         | Nama tiap test ditampilkan satu per satu                     |

Efek samping test integrasi: [AppFactory.cs](../tests/UptimeMonitor.Tests/AppFactory.cs#L83) membuat database sementara `UptimeMonitor_Test_<guid>` lalu menghapusnya setelah selesai. Database asli `UptimeMonitor` tidak tersentuh.

---

## Tahap 9: Penutup

|     | Command                                        | Menghasilkan                                                       |
| --- | ---------------------------------------------- | ------------------------------------------------------------------ |
| ✔   | `git add` lalu `git commit -m "..."` per tahap | Sembilan commit yang terlihat di `git log`                         |
| ✔   | `git log --reverse --format='%h %s'`           | Daftar commit dari yang paling lama                                |
| ✔   | `git show --stat <hash>`                       | File apa saja yang ditambahkan di satu commit, tanpa pindah commit |

---

## Urutan inti, kalau ingin mengulang dari nol

Coba di folder kosong di luar repo ini, lalu bandingkan hasilnya dengan repo ini.

```powershell
dotnet new sln --format slnx -n UptimeMonitor
dotnet new blazor -n UptimeMonitor.Web -o src/UptimeMonitor.Web --interactivity Server
dotnet new xunit -n UptimeMonitor.Tests -o tests/UptimeMonitor.Tests
dotnet sln add src/UptimeMonitor.Web tests/UptimeMonitor.Tests
dotnet add tests/UptimeMonitor.Tests reference src/UptimeMonitor.Web
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.15
dotnet ef migrations add InitialCreate --project src/UptimeMonitor.Web --output-dir Data/Migrations
dotnet test
dotnet run --project src/UptimeMonitor.Web --launch-profile http
```
