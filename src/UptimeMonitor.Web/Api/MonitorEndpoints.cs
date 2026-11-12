using Microsoft.AspNetCore.Http.HttpResults;
using UptimeMonitor.Web.Monitoring;

namespace UptimeMonitor.Web.Api;

public static class MonitorEndpoints
{
    public static IEndpointRouteBuilder MapMonitorApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/status", (StatusQueries queries, CancellationToken cancellationToken) =>
                queries.GetOverviewAsync(cancellationToken))
            .WithName("GetStatus")
            .WithTags("Status");

        var monitors = app.MapGroup("/api/monitors").RequireAuthorization().WithTags("Monitors");

        monitors.MapGet("/", (MonitorService service, CancellationToken cancellationToken) =>
                service.ListAsync(cancellationToken))
            .WithName("ListMonitors");

        monitors.MapGet("/{id:int}", GetMonitor).WithName("GetMonitor");
        monitors.MapPost("/", CreateMonitor).WithName("CreateMonitor");
        monitors.MapPut("/{id:int}", UpdateMonitor).WithName("UpdateMonitor");
        monitors.MapDelete("/{id:int}", DeleteMonitor).WithName("DeleteMonitor");
        monitors.MapPost("/{id:int}/pause", PauseMonitor).WithName("PauseMonitor");
        monitors.MapPost("/{id:int}/resume", ResumeMonitor).WithName("ResumeMonitor");
        monitors.MapGet("/{id:int}/checks", ListChecks).WithName("ListMonitorChecks");

        return app;
    }

    private static async Task<Results<Ok<EndpointMonitor>, NotFound>> GetMonitor(
        int id,
        MonitorService service,
        CancellationToken cancellationToken) =>
        await service.FindAsync(id, cancellationToken) is { } monitor
            ? TypedResults.Ok(monitor)
            : TypedResults.NotFound();

    private static async Task<Created<EndpointMonitor>> CreateMonitor(
        MonitorInput input,
        MonitorService service,
        CancellationToken cancellationToken)
    {
        var monitor = await service.CreateAsync(input, cancellationToken);
        return TypedResults.Created($"/api/monitors/{monitor.Id}", monitor);
    }

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

    private static async Task<Ok<List<CheckResult>>> ListChecks(
        int id,
        MonitorService service,
        CancellationToken cancellationToken,
        int take = 50) =>
        TypedResults.Ok(await service.RecentChecksAsync(id, take, cancellationToken));
}
