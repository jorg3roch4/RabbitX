using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitX.Configuration;
using RabbitX.Connection;

namespace RabbitX.HealthChecks;

/// <summary>
/// Extension methods for adding RabbitX health checks.
/// </summary>
public static class HealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a health check for the RabbitMQ connection.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="configure">Optional action to configure health check options.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddRabbitX(
        this IHealthChecksBuilder builder,
        Action<RabbitMQHealthCheckOptions>? configure = null)
    {
        var options = new RabbitMQHealthCheckOptions();
        configure?.Invoke(options);

        return builder.Add(new HealthCheckRegistration(
            name: options.Name,
            factory: sp =>
            {
                var connection = sp.GetRequiredService<IRabbitMQConnection>();
                var rabbitXOptions = sp.GetRequiredService<RabbitXOptions>();
                var logger = sp.GetRequiredService<ILogger<RabbitMQHealthCheck>>();
                return new RabbitMQHealthCheck(connection, rabbitXOptions, logger);
            },
            failureStatus: options.FailureStatus,
            tags: options.Tags,
            timeout: options.Timeout));
    }
}
