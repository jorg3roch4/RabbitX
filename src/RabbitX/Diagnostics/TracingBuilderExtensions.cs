using OpenTelemetry.Trace;

namespace RabbitX.Diagnostics;

/// <summary>
/// Extension methods for registering RabbitX tracing instrumentation with OpenTelemetry.
/// </summary>
public static class TracingBuilderExtensions
{
    /// <summary>
    /// Adds RabbitX distributed tracing instrumentation to the <see cref="TracerProviderBuilder"/>.
    /// Subscribes to the "RabbitX" ActivitySource to capture publish, consume, and RPC spans.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
    /// <returns>The <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddRabbitXInstrumentation(this TracerProviderBuilder builder)
    {
        return builder.AddSource(RabbitXTelemetryConstants.ActivitySourceName);
    }
}
