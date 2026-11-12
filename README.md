# Uptime Monitor

A small uptime monitoring service with a public status page, built with .NET 10.

A background worker checks registered HTTP endpoints on their own interval, records the status and latency of every check, opens an incident after several consecutive failures, and resolves it when the endpoint recovers. Results are shown on a public status page and managed from an admin dashboard.

## Features

- Monitors with a configurable URL, check interval, timeout, and expected status code
- Background checks that run concurrently and never overlap for the same monitor
- Automatic incidents: opened after 3 consecutive failures (configurable), resolved on recovery
- Public status page with uptime over 24 hours, 7 days, and 30 days
- Admin dashboard with create, edit, pause, resume, delete, latency chart, and recent checks
- JSON API with an OpenAPI document
- English and Indonesian user interface
- Automatic removal of check results older than 30 days

## Tech stack

| Area | Technology |
| --- | --- |
| Runtime | .NET 10 |
| Web | ASP.NET Core Minimal API, Blazor (static SSR and interactive server) |
| Background work | `BackgroundService` with `PeriodicTimer` |
| Data | Entity Framework Core 10, SQL Server |
| Auth | Cookie authentication with a single admin account |
| Localization | `IStringLocalizer` with `.resx` resources |
| Tests | xUnit, `WebApplicationFactory`, `FakeTimeProvider` |

## Project structure

```
src/UptimeMonitor.Web/
  Monitoring/    domain entities, endpoint checker, check cycle, scheduler, queries
  Data/          DbContext and migrations
  Api/           Minimal API endpoints
  Auth/          login, logout, and language endpoints
  Components/    Blazor layout, pages, and shared components
  Resources/     English and Indonesian texts
tests/UptimeMonitor.Tests/
```

## How it works

1. `MonitorScheduler` wakes up every few seconds and runs one `MonitorCheckCycle`.
2. The cycle loads the monitors that are due, checks them in parallel with `EndpointChecker`, and stores one `CheckResult` per monitor.
3. `EndpointMonitor.Register` updates the monitor state and decides whether an incident opens or resolves.
4. `StatusQueries` aggregates the stored results into uptime percentages for the status page and the API.

## Running locally

Requirements: .NET 10 SDK and a SQL Server instance. The default connection string targets `localhost\SQLEXPRESS` with Windows authentication.

```
dotnet run --project src/UptimeMonitor.Web --launch-profile http
```

The database is created and migrated on startup. Open `http://localhost:5083` for the status page and `http://localhost:5083/admin` for the dashboard.

In the Development environment the admin account is `admin` / `admin123`. For any other environment set the credentials and connection string through configuration:

```
Admin__Username=...
Admin__Password=...
ConnectionStrings__Default=...
```

## API

| Method | Path | Access | Description |
| --- | --- | --- | --- |
| GET | `/api/status` | Public | Monitors with uptime and recent incidents |
| GET | `/api/monitors` | Admin | List monitors |
| GET | `/api/monitors/{id}` | Admin | Get one monitor |
| POST | `/api/monitors` | Admin | Create a monitor |
| PUT | `/api/monitors/{id}` | Admin | Update a monitor |
| DELETE | `/api/monitors/{id}` | Admin | Delete a monitor and its history |
| POST | `/api/monitors/{id}/pause` | Admin | Pause checks |
| POST | `/api/monitors/{id}/resume` | Admin | Resume checks |
| GET | `/api/monitors/{id}/checks` | Admin | Recent check results |

The OpenAPI document is served at `/openapi/v1.json`.

## Configuration

| Key | Default | Description |
| --- | --- | --- |
| `Monitoring:TickSeconds` | 5 | How often the scheduler looks for due monitors |
| `Monitoring:FailureThreshold` | 3 | Consecutive failures before an incident opens |
| `Monitoring:RetentionDays` | 30 | How long check results are kept |

## Tests

```
dotnet test
```

Unit tests cover the incident rules, scheduling rules, uptime calculation, and the endpoint checker. Integration tests run the real application against a temporary SQL Server database, which is created per test class and dropped afterwards. Set `UPTIMEMONITOR_TEST_SQLSERVER` to use an instance other than `localhost\SQLEXPRESS`.
