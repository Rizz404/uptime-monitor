# Alur pembuatan file, selangkah demi selangkah

Urutan ini mengikuti cara aplikasi seperti ini biasanya dibangun: **dari inti ke luar**.

```
aturan bisnis → database → logika → API → tampilan → test integrasi
```

Alasannya: bagian dalam (aturan bisnis) adalah yang paling pasti dan paling dibutuhkan bagian lain. Tampilan dikerjakan terakhir karena ia hanya "memakai" semua yang sudah jadi.

Setiap langkah berakhir di titik di mana kamu bisa **membuktikan bahwa hasilnya jalan** (`dotnet build`, `dotnet test`, atau menjalankan aplikasi). Jangan lanjut ke langkah berikutnya sebelum titik itu lulus.

Kolom "Commit referensi" menunjuk commit di git history yang berisi file itu, sebagai pembanding lewat `git show --stat <hash>`. Urutan di dokumen ini sedikit berbeda dari urutan commit, dan perbedaannya dijelaskan di akhir.

## Tiga hal yang pasti terjadi saat membangun

Ketiganya normal. Jangan mengira ada yang salah.

1. **File sering dibuat setengah dulu, lalu dilengkapi.** `Program.cs`, `appsettings.json`, `SharedResource.resx`, dan `app.css` dibuka berkali-kali, bukan ditulis sekali jadi.
2. **Dua file kadang lahir berpasangan**, karena yang satu langsung membutuhkan yang lain. Contoh: `EndpointMonitor` dan `CheckOutcome`.
3. **Teks `.resx` tumbuh mengikuti UI.** Tiap halaman baru menambah key. Key yang belum ada tampil di layar sebagai nama key-nya, bukan error.

---

## Langkah 1: Kerangka

| | |
|---|---|
| **File** | `UptimeMonitor.slnx`, dua `.csproj`, `dotnet-tools.json`, `.gitignore`, `launchSettings.json` |
| **Cara** | `dotnet new ...`, lihat [command-alur.md](command-alur.md) |
| **Cek** | `dotnet build` sukses |
| **Commit referensi** | `c44b80f` |

Template `dotnet new blazor` sudah membuat `Program.cs`, folder `Components/`, dan `wwwroot/app.css` versi bawaan. Jadi project bisa di-build dan dijalankan sejak langkah ini. File-file itu nanti diganti isinya sedikit demi sedikit.

---

## Langkah 2: Aturan bisnis (inti aplikasi)

Pertanyaan pertama yang harus dijawab: **apa itu "monitor", dan kapan ia dianggap down?** Belum perlu database, HTTP, atau tampilan.

| Urutan | File | Isi |
|---|---|---|
| 1 | [EndpointMonitor.cs](../src/UptimeMonitor.Web/Monitoring/EndpointMonitor.cs) | Data monitor (nama, URL, interval), status (`Up`/`Down`/`Pending`/`Paused`), dan method `Register(...)` yang menghitung gagal berturut-turut sampai insiden dibuka |
| 2 | [CheckResult.cs](../src/UptimeMonitor.Web/Monitoring/CheckResult.cs) | `CheckOutcome` (hasil satu pengecekan), `CheckResult` (riwayat), `Incident` |

**Kenapa dua file sekaligus:** saat menulis `Register(CheckOutcome outcome, ...)` di `EndpointMonitor`, kamu langsung butuh tipe `CheckOutcome`. Saat itu juga kamu membuatnya, dan sekalian dua kelas lain yang sejenis.

| | |
|---|---|
| **Cek** | `dotnet build` |
| **Bonus** | Tulis [EndpointMonitorTests.cs](../tests/UptimeMonitor.Tests/EndpointMonitorTests.cs) sekarang. Logika ini murni (tanpa database/HTTP), jadi paling mudah diuji, dan test-nya membuktikan aturan "3 gagal = insiden" benar sebelum ada hal lain. Jalankan `dotnet test` |
| **Commit referensi** | `413a691` (test-nya baru di `194f03d`) |

---

## Langkah 3: Database

Aturan sudah ada, sekarang pikirkan bagaimana menyimpannya.

| Urutan | File / perintah | Isi |
|---|---|---|
| 1 | [AppDbContext.cs](../src/UptimeMonitor.Web/Data/AppDbContext.cs) | Tiga tabel (`Monitors`, `CheckResults`, `Incidents`) dari tiga entity di Langkah 2, plus index dan relasi |
| 2 | [appsettings.json](../src/UptimeMonitor.Web/appsettings.json) | Tambah `ConnectionStrings:Default` |
| 3 | [Program.cs](../src/UptimeMonitor.Web/Program.cs) | Tambah satu blok: `AddDbContextFactory<AppDbContext>(... UseSqlServer ...)` |
| 4 | Perintah | `dotnet tool restore`, lalu `dotnet ef migrations add InitialCreate --project src/UptimeMonitor.Web --output-dir Data/Migrations` |
| 5 | Folder [Data/Migrations/](../src/UptimeMonitor.Web/Data/Migrations/) | 3 file dihasilkan otomatis. Jangan ditulis tangan |

**Kenapa `Program.cs` dibuka di sini:** perintah `dotnet ef` mencari `AppDbContext` lewat `Program.cs`. Repo ini tidak punya `IDesignTimeDbContextFactory`, jadi `AddDbContextFactory` harus sudah terdaftar sebelum migration dibuat. Ini kesimpulan dari kode, belum saya coba tanpa blok itu.

Di tahap ini cukup tambahkan blok database saja. Sisanya ditambahkan nanti, sedikit demi sedikit.

| | |
|---|---|
| **Cek** | `dotnet ef migrations list --project src/UptimeMonitor.Web` menampilkan `InitialCreate`. Menjalankan aplikasi, atau `dotnet ef database update`, membuat database `UptimeMonitor` di SQL Server Express |
| **Commit referensi** | `413a691` (`AppDbContext`) dan `a51506a` (migration) |

---

## Langkah 4: Mengecek satu endpoint

Sekarang aplikasi tahu apa itu monitor. Langkah berikutnya: **bagaimana benar-benar mengecek satu URL?**

| Urutan | File | Isi |
|---|---|---|
| 1 | [EndpointChecker.cs](../src/UptimeMonitor.Web/Monitoring/EndpointChecker.cs) | Kirim HTTP GET, ukur latency, tangani timeout dan error, kembalikan `CheckOutcome` |
| 2 | [StubHttpHandler.cs](../tests/UptimeMonitor.Tests/StubHttpHandler.cs) | HTTP palsu untuk test |
| 3 | [EndpointCheckerTests.cs](../tests/UptimeMonitor.Tests/EndpointCheckerTests.cs) | Test: status cocok, status beda, timeout, error koneksi |

| | |
|---|---|
| **Cek** | `dotnet test`. Unit test ini tidak butuh database |
| **Commit referensi** | `413a691` (checker) dan `194f03d` (test) |

---

## Langkah 5: Pengecekan otomatis di latar belakang

Satu pengecekan sudah bisa. Sekarang buat agar berjalan sendiri dan hasilnya tersimpan.

| Urutan | File | Isi |
|---|---|---|
| 1 | [MonitoringOptions.cs](../src/UptimeMonitor.Web/Monitoring/MonitoringOptions.cs) | Setting: `TickSeconds`, `FailureThreshold`, `RetentionDays` |
| 2 | [MonitorCheckCycle.cs](../src/UptimeMonitor.Web/Monitoring/MonitorCheckCycle.cs) | Satu putaran: ambil monitor jatuh tempo → cek semuanya → simpan `CheckResult` → buka/tutup `Incident` → hapus data lama |
| 3 | [MonitorScheduler.cs](../src/UptimeMonitor.Web/Monitoring/MonitorScheduler.cs) | `BackgroundService` yang memanggil putaran itu tiap `TickSeconds` |
| 4 | `appsettings.json` | Tambah section `Monitoring` |
| 5 | `Program.cs` | Daftarkan semuanya: `Configure<MonitoringOptions>`, `AddSingleton(TimeProvider.System)`, `AddHttpClient<EndpointChecker>`, `AddScoped<MonitorCheckCycle>`, `AddHostedService<MonitorScheduler>`, dan `Database.MigrateAsync()` saat start |

| | |
|---|---|
| **Cek** | Jalankan aplikasi. Log harus bersih dari error. Belum ada monitor untuk dicek, karena belum ada cara menambahnya. Itu urusan langkah berikutnya |
| **Commit referensi** | `413a691` dan `b54792e` (isi akhir `Program.cs`) |

---

## Langkah 6: Menambah dan mengelola monitor

Sekarang buat cara untuk memasukkan monitor ke sistem.

Di langkah ini lokalisasi pertama kali dibutuhkan, karena **input dari pengguna perlu pesan validasi** ("URL tidak valid", dst.). Jadi file `.resx` dibuat di sini, bukan di awal.

| Urutan | File | Isi |
|---|---|---|
| 1 | [SharedResource.resx](../src/UptimeMonitor.Web/Resources/SharedResource.resx) dan [.id.resx](../src/UptimeMonitor.Web/Resources/SharedResource.id.resx) | Mulai dengan 4 key `Validation_*` saja (EN dan ID) |
| 2 | [ValidationMessages.cs](../src/UptimeMonitor.Web/Localization/ValidationMessages.cs) | Mengambil teks dari resx, dan atribut `[HttpUrl]` |
| 3 | [MonitorInput.cs](../src/UptimeMonitor.Web/Monitoring/MonitorInput.cs) | Bentuk data dari form/API beserta aturan validasinya, dan konversi dari/ke `EndpointMonitor` |
| 4 | [MonitorService.cs](../src/UptimeMonitor.Web/Monitoring/MonitorService.cs) | Tambah, ubah, jeda, hapus, daftar, dan riwayat pengecekan |
| 5 | `Program.cs` | `AddScoped<MonitorService>()` |

| | |
|---|---|
| **Cek** | `dotnet build`. Belum bisa dicoba dari luar, karena belum ada endpoint atau halaman |
| **Commit referensi** | `a866096` (localization), `413a691` (`MonitorInput`, `MonitorService`) |

---

## Langkah 7: Data untuk status page

| Urutan | File | Isi |
|---|---|---|
| 1 | [StatusQueries.cs](../src/UptimeMonitor.Web/Monitoring/StatusQueries.cs) | Ringkasan semua monitor, persentase uptime 24 jam/7 hari/30 hari, dan 10 insiden terakhir |
| 2 | `Program.cs` | `AddScoped<StatusQueries>()` |

| | |
|---|---|
| **Cek** | `dotnet build` |
| **Commit referensi** | `413a691` |

---

## Langkah 8: Login admin

Dashboard harus dilindungi sebelum dibuat, jadi auth dikerjakan **sebelum** API dan UI yang memakainya.

| Urutan | File | Isi |
|---|---|---|
| 1 | [AdminOptions.cs](../src/UptimeMonitor.Web/Auth/AdminOptions.cs) | Username dan password admin, dengan pembandingan aman (waktu konstan) |
| 2 | [AuthEndpoints.cs](../src/UptimeMonitor.Web/Auth/AuthEndpoints.cs) | `POST /auth/login`, `POST /auth/logout`, `GET /culture/{culture}`, dan `SupportedCultures` |
| 3 | `appsettings.json` dan [appsettings.Development.json](../src/UptimeMonitor.Web/appsettings.Development.json) | Section `Admin` (kosong di file utama, diisi di Development) |
| 4 | `Program.cs` | `AddAuthentication().AddCookie(...)`, `AddAuthorization()`, `AddLocalization`, `UseRequestLocalization`, `UseAuthentication`, `UseAuthorization`, `MapAuthEndpoints()` |

| | |
|---|---|
| **Cek** | Jalankan aplikasi, lalu kirim `POST /auth/login` (bisa lewat `curl`) dan lihat balasan redirect |
| **Commit referensi** | `5b51c9f` |

---

## Langkah 9: API

| Urutan | File | Isi |
|---|---|---|
| 1 | [MonitorEndpoints.cs](../src/UptimeMonitor.Web/Api/MonitorEndpoints.cs) | `GET /api/status` (publik), dan CRUD `/api/monitors/...` (wajib login) |
| 2 | `Program.cs` | `AddOpenApi()`, `AddValidation()`, `MapOpenApi()`, `MapMonitorApi()`, dan converter enum JSON |

| | |
|---|---|
| **Cek** | Jalankan aplikasi. Buka `http://localhost:5083/api/status` (JSON kosong pun tidak apa) dan `http://localhost:5083/openapi/v1.json` |
| **Commit referensi** | `b54792e` |

Setelah langkah ini, **seluruh backend sudah selesai dan bisa dipakai**, bahkan tanpa UI. `Program.cs` juga sudah hampir final.

---

## Langkah 10: Kerangka tampilan

Sebelum membuat halaman, siapkan "bingkai" tempat halaman tampil.

| Urutan | File | Isi |
|---|---|---|
| 1 | [SharedResource.cs](../src/UptimeMonitor.Web/SharedResource.cs) | Class kosong sebagai penanda localizer UI |
| 2 | [_Imports.razor](../src/UptimeMonitor.Web/Components/_Imports.razor) | `@using` bersama, dan `@inject IStringLocalizer<SharedResource> L` yang membuat `@L["..."]` bisa dipakai di semua komponen |
| 3 | [MainLayout.razor](../src/UptimeMonitor.Web/Components/Layout/MainLayout.razor) | Header, menu, tombol login/logout, pengalih bahasa EN/ID |
| 4 | [RedirectToLogin.razor](../src/UptimeMonitor.Web/Components/Shared/RedirectToLogin.razor) | Mengalihkan pengguna yang belum login |
| 5 | [Routes.razor](../src/UptimeMonitor.Web/Components/Routes.razor) | Router, memakai `MainLayout`, `NotFound`, dan `RedirectToLogin` |
| 6 | [App.razor](../src/UptimeMonitor.Web/Components/App.razor) | Dokumen HTML utama |
| 7 | [wwwroot/app.css](../src/UptimeMonitor.Web/wwwroot/app.css) | Mulai kecil, ditambah seiring halaman dibuat |

`ReconnectModal.razor` (dan `.css`, `.js`), `NotFound.razor`, dan `Error.razor` datang dari template, hanya diubah sedikit.

| | |
|---|---|
| **Cek** | Jalankan aplikasi dan buka `/`. Header tampil walau isinya masih kosong. Klik EN/ID dan lihat bahasa menu berubah |
| **Commit referensi** | `27554b5` |

---

## Langkah 11: Halaman, dari yang paling sederhana

| Urutan | File | Isi |
|---|---|---|
| 1 | [Display.cs](../src/UptimeMonitor.Web/Components/Shared/Display.cs) dan [StatusBadge.razor](../src/UptimeMonitor.Web/Components/Shared/StatusBadge.razor) | Helper format dan lencana status |
| 2 | [Status.razor](../src/UptimeMonitor.Web/Components/Pages/Status.razor) | Status page publik. Memakai `StatusQueries`, `StatusBadge`, `Display` |
| 3 | [Login.razor](../src/UptimeMonitor.Web/Components/Pages/Login.razor) | Form yang mengirim ke `/auth/login` |
| 4 | [LatencyChart.razor](../src/UptimeMonitor.Web/Components/Shared/LatencyChart.razor) | Grafik SVG latency |
| 5 | [Admin.razor](../src/UptimeMonitor.Web/Components/Pages/Admin.razor) | Dashboard: tabel monitor, tambah/edit/jeda/hapus, detail dengan grafik. Yang paling kompleks, jadi terakhir |

Setiap kali menulis teks di halaman, tambahkan key-nya ke kedua file `.resx`.

| | |
|---|---|
| **Cek** | Jalankan aplikasi. Setelah Status selesai, buka `/` (kosong). Setelah Login dan Admin selesai, login, tambah monitor, dan lihat statusnya berubah dari "Pending" ke "Operational" |
| **Commit referensi** | `27554b5` |

Langkah ini adalah satu-satunya yang paling baik dicek langsung di browser.

---

## Langkah 12: Test integrasi

Terakhir, buktikan seluruh aplikasi bekerja bersama.

| Urutan | File | Isi |
|---|---|---|
| 1 | [AppFactory.cs](../tests/UptimeMonitor.Tests/AppFactory.cs) | Menyalakan aplikasi untuk test, dengan HTTP palsu, waktu palsu, dan database sementara |
| 2 | [IntegrationTests.cs](../tests/UptimeMonitor.Tests/IntegrationTests.cs) | Menguji API dan login lewat aplikasi sungguhan |

| | |
|---|---|
| **Cek** | `dotnet test`. Butuh SQL Server Express aktif |
| **Commit referensi** | `194f03d` |

---

## Langkah 13: README

[README.md](../README.md). Commit `5ed4d18`.

---

## Perbedaan dengan urutan commit di git

Commit adalah pengelompokan hasil akhir, sedangkan urutan di dokumen ini adalah urutan kerjanya. Perbedaan utamanya:

| Di dokumen ini | Di git history |
|---|---|
| Resource `.resx` dibuat di Langkah 6, saat `MonitorInput` butuh pesan validasi | Commit `a866096` (commit ke-2), sebelum monitoring |
| Migration (Langkah 3) dibuat setelah `AppDbContext` dan blok database di `Program.cs` | Commit `a51506a` terpisah. `Program.cs` baru ter-commit di `b54792e` setelah dilengkapi |
| Auth (Langkah 8) dikerjakan sebelum API (Langkah 9), karena API memakai `RequireAuthorization()` | Sama: `5b51c9f` lalu `b54792e` |
| Unit test ditulis berdampingan dengan logikanya (Langkah 2 dan 4) | Semua test di satu commit `194f03d` |
| `MonitorService` dan `StatusQueries` dibuat tepat saat dibutuhkan (Langkah 6 dan 7) | Keduanya di commit `413a691` bersama semua file monitoring |

Dugaan tentang isi `Program.cs` dan urutan pengerjaan aslinya tidak bisa dibuktikan dari git history, karena commit hanya memotret hasil akhirnya. Jadi dokumen ini adalah **urutan yang masuk akal untuk dikerjakan**, bukan rekaman persis apa yang terjadi dulu.
