using OpenTelemetry.Metrics;

namespace RabbitX.Diagnostics;

/// <summary>
/// Extension methods for registering RabbitX metrics instrumentation with OpenTelemetry.
/// </summary>
public static class MetricsBuilderExtensions
{
    /// <summary>
    /// Adds RabbitX metrics instrumentation to the <see cref="MeterProviderBuilder"/>.
    /// Subscribes to the "RabbitX" Meter to capture publish, consume, RPC, and connection metrics.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
    /// <returns>The <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddRabbitXInstrumentation(this MeterProviderBuilder builder)
    {
        return builder.AddMeter(RabbitXTelemetryConstants.MeterName);
    }
}
