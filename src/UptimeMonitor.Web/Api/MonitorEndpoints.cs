using Microsoft.AspNetCore.Http.HttpResults;
using UptimeMonitor.Web.Monitoring;

namespace UptimeMonitor.Web.Api;

public static class MonitorEndpoints
{
    public static IEndpointRouteBuilder MapMonitorApi(this IEndpointRouteBuilder app)
    {
        // * Satu-satunya endpoint publik (tanpa login), dipakai status page
        // ? WithName = nama operasi di dokumen OpenAPI; WithTags = pengelompokan di dokumen itu
        app.MapGet("/api/status", (StatusQueries queries, CancellationToken cancellationToken) =>
                queries.GetOverviewAsync(cancellationToken))
            .WithName("GetStatus")
            .WithTags("Status");

        // * MapGroup: awalan "/api/monitors" dan RequireAuthorization berlaku untuk semua rute di bawahnya, jadi tidak ditulis satu-satu
        var monitors = app.MapGroup("/api/monitors").RequireAuthorization().WithTags("Monitors");

        monitors.MapGet("/", (MonitorService service, CancellationToken cancellationToken) =>
                service.ListAsync(cancellationToken))
            .WithName("ListMonitors");

        // * Pola REST: GET = baca, POST = buat, PUT = ubah, DELETE = hapus. "{id:int}" hanya cocok untuk angka bulat
        monitors.MapGet("/{id:int}", GetMonitor).WithName("GetMonitor");
        monitors.MapPost("/", CreateMonitor).WithName("CreateMonitor");
        monitors.MapPut("/{id:int}", UpdateMonitor).WithName("UpdateMonitor");
        monitors.MapDelete("/{id:int}", DeleteMonitor).WithName("DeleteMonitor");
        // ? Jeda/lanjut memakai POST ke sub-rute karena berupa "aksi", bukan mengubah seluruh data
        monitors.MapPost("/{id:int}/pause", PauseMonitor).WithName("PauseMonitor");
        monitors.MapPost("/{id:int}/resume", ResumeMonitor).WithName("ResumeMonitor");
        monitors.MapGet("/{id:int}/checks", ListChecks).WithName("ListMonitorChecks");

        return app;
    }

    // * Results<Ok<..>, NotFound> di tipe return mendaftar semua kemungkinan respons: compiler ikut memeriksa, dan OpenAPI mencatatnya otomatis
    // ? "is { } monitor" = bila hasilnya tidak null, simpan di variabel monitor
    private static async Task<Results<Ok<EndpointMonitor>, NotFound>> GetMonitor(
        int id,
        MonitorService service,
        CancellationToken cancellationToken) =>
        await service.FindAsync(id, cancellationToken) is { } monitor
            ? TypedResults.Ok(monitor)
            : TypedResults.NotFound();

    // * MonitorInput dibaca dari body JSON, dan validasinya dijalankan sebelum method ini (data tidak valid langsung dijawab 400)
    // * Created = 201 beserta alamat resource barunya
    private static async Task<Created<EndpointMonitor>> CreateMonitor(
        MonitorInput input,
        MonitorService service,
        CancellationToken cancellationToken)
    {
        var monitor = await service.CreateAsync(input, cancellationToken);
        return TypedResults.Created($"/api/monitors/{monitor.Id}", monitor);
    }

    // * Pola sama untuk empat method di bawah: service mengembalikan bool, true menjadi 204 NoContent (berhasil tanpa isi), false menjadi 404
    private static async Task<Results<NoContent, NotFound>> UpdateMonitor(
        int id,
        MonitorInput input,
        MonitorService service,
        CancellationToken cancellationToken) =>
        await service.UpdateAsync(id, input, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<Results<NoContent, NotFound>> DeleteMonitor(
        int id,
        MonitorService service,
        CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<Results<NoContent, NotFound>> PauseMonitor(
        int id,
        MonitorService service,
        CancellationToken cancellationToken) =>
        await service.SetPausedAsync(id, true, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<Results<NoContent, NotFound>> ResumeMonitor(
        int id,
        MonitorService service,
        CancellationToken cancellationToken) =>
        await service.SetPausedAsync(id, false, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    // ? "take = 50" = parameter query string opsional (?take=20), batas 1 sampai 200 dijaga di service
    private static async Task<Ok<List<CheckResult>>> ListChecks(
        int id,
        MonitorService service,
        CancellationToken cancellationToken,
        int take = 50) =>
        TypedResults.Ok(await service.RecentChecksAsync(id, take, cancellationToken));
}
