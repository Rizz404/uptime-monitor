using System.ComponentModel.DataAnnotations;
using UptimeMonitor.Web.Localization;

namespace UptimeMonitor.Web.Monitoring;

public class MonitorInput
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Required))]
    [StringLength(100, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_MaxLength))]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Required))]
    [StringLength(2000, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_MaxLength))]
    [HttpUrl]
    public string Url { get; set; } = string.Empty;

    [Range(30, 86400, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Range))]
    public int IntervalSeconds { get; set; } = 60;

    [Range(1, 60, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Range))]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(100, 599, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Range))]
    public int ExpectedStatusCode { get; set; } = 200;

    public static MonitorInput From(EndpointMonitor monitor) => new()
    {
        Name = monitor.Name,
        Url = monitor.Url,
        IntervalSeconds = monitor.IntervalSeconds,
        TimeoutSeconds = monitor.TimeoutSeconds,
        ExpectedStatusCode = monitor.ExpectedStatusCode
    };

    public void ApplyTo(EndpointMonitor monitor)
    {
        monitor.Name = Name.Trim();
        monitor.Url = Url.Trim();
        monitor.IntervalSeconds = IntervalSeconds;
        monitor.TimeoutSeconds = TimeoutSeconds;
        monitor.ExpectedStatusCode = ExpectedStatusCode;
    }
}
