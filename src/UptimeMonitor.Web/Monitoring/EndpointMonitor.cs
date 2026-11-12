namespace UptimeMonitor.Web.Monitoring;

public class EndpointMonitor
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Url { get; set; }
    public int IntervalSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
    public int ExpectedStatusCode { get; set; } = 200;
    public bool IsPaused { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public bool? LastCheckSucceeded { get; private set; }
    public int? LastLatencyMs { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public bool HasOpenIncident { get; private set; }

    public MonitorStatus Status => this switch
    {
        { IsPaused: true } => MonitorStatus.Paused,
        { LastCheckSucceeded: null } => MonitorStatus.Pending,
        { LastCheckSucceeded: true } => MonitorStatus.Up,
        _ => MonitorStatus.Down
    };

    public bool IsDue(DateTimeOffset now) =>
        !IsPaused && (LastCheckedAt is null || LastCheckedAt.Value.AddSeconds(IntervalSeconds) <= now);

    public IncidentChange Register(CheckOutcome outcome, DateTimeOffset checkedAt, int failureThreshold)
    {
        LastCheckedAt = checkedAt;
        LastCheckSucceeded = outcome.IsUp;
        LastLatencyMs = outcome.LatencyMs;

        if (outcome.IsUp)
        {
            ConsecutiveFailures = 0;
            if (!HasOpenIncident)
            {
                return IncidentChange.None;
            }

            HasOpenIncident = false;
            return IncidentChange.Resolved;
        }

        ConsecutiveFailures++;
        if (HasOpenIncident || ConsecutiveFailures < failureThreshold)
        {
            return IncidentChange.None;
        }

        HasOpenIncident = true;
        return IncidentChange.Opened;
    }
}

public enum MonitorStatus
{
    Pending,
    Up,
    Down,
    Paused
}

public enum IncidentChange
{
    None,
    Opened,
    Resolved
}
