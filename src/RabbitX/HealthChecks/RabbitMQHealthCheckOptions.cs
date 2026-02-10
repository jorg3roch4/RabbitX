using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RabbitX.HealthChecks;

/// <summary>
/// Configuration options for the RabbitMQ health check.
/// </summary>
public sealed class RabbitMQHealthCheckOptions
{
    /// <summary>
    /// Gets or sets the name of the health check. Default is "rabbitmq".
    /// </summary>
    public string Name { get; set; } = "rabbitmq";

    /// <summary>
    /// Gets or sets the tags associated with this health check.
    /// </summary>
    public IEnumerable<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="HealthStatus"/> that should be reported when the health check fails.
    /// If null, the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </summary>
    public HealthStatus? FailureStatus { get; set; }

    /// <summary>
    /// Gets or sets the timeout for the health check operation. Default is 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
