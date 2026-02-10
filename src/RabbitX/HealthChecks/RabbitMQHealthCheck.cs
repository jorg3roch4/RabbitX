using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitX.Configuration;
using RabbitX.Connection;

namespace RabbitX.HealthChecks;

/// <summary>
/// Health check for RabbitMQ connection status.
/// </summary>
public sealed class RabbitMQHealthCheck : IHealthCheck
{
    private readonly IRabbitMQConnection _connection;
    private readonly RabbitXOptions _options;
    private readonly ILogger<RabbitMQHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMQHealthCheck"/> class.
    /// </summary>
    /// <param name="connection">The RabbitMQ connection.</param>
    /// <param name="options">The RabbitX configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public RabbitMQHealthCheck(
        IRabbitMQConnection connection,
        RabbitXOptions options,
        ILogger<RabbitMQHealthCheck> logger)
    {
        _connection = connection;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Fast-fail: check if connection is established
            if (!_connection.IsConnected)
            {
                _logger.LogWarning("RabbitMQ health check failed: connection is not open");
                return HealthCheckResult.Unhealthy(
                    "RabbitMQ connection is not open",
                    data: CreateBasicData());
            }

            // Check if connection is blocked (degraded state)
            if (_connection.IsBlocked)
            {
                _logger.LogWarning("RabbitMQ health check degraded: connection is blocked");
                return HealthCheckResult.Degraded(
                    "RabbitMQ connection is blocked by broker",
                    data: CreateBasicData(isBlocked: true));
            }

            // Verify actual communication by creating a temporary channel
            IChannel? channel = null;
            try
            {
                channel = await _connection.CreateChannelAsync(
                    enablePublisherConfirms: false,
                    cancellationToken);

                // Get connection details for health data
                var connection = await _connection.GetConnectionAsync(cancellationToken);
                var data = CreateHealthyData(connection);

                _logger.LogDebug("RabbitMQ health check passed");
                return HealthCheckResult.Healthy("RabbitMQ connection is healthy", data);
            }
            finally
            {
                if (channel is not null)
                {
                    try
                    {
                        await channel.CloseAsync(cancellationToken);
                        channel.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error closing health check channel");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "RabbitMQ health check timed out",
                data: CreateBasicData());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ health check failed with exception");
            return HealthCheckResult.Unhealthy(
                $"RabbitMQ health check failed: {ex.Message}",
                ex,
                CreateBasicData());
        }
    }

    private Dictionary<string, object> CreateBasicData(bool isBlocked = false)
    {
        return new Dictionary<string, object>
        {
            ["host"] = _options.Connection.HostName,
            ["port"] = _options.Connection.Port,
            ["virtualHost"] = _options.Connection.VirtualHost,
            ["isBlocked"] = isBlocked
        };
    }

    private Dictionary<string, object> CreateHealthyData(IConnection connection)
    {
        var data = new Dictionary<string, object>
        {
            ["host"] = _options.Connection.HostName,
            ["port"] = _options.Connection.Port,
            ["virtualHost"] = _options.Connection.VirtualHost,
            ["clientName"] = connection.ClientProvidedName ?? "unknown",
            ["isBlocked"] = _connection.IsBlocked
        };

        // Add server properties if available
        var serverProperties = connection.ServerProperties;
        if (serverProperties is not null)
        {
            if (serverProperties.TryGetValue("product", out var product) && product is byte[] productBytes)
            {
                data["server"] = Encoding.UTF8.GetString(productBytes);
            }

            if (serverProperties.TryGetValue("version", out var version) && version is byte[] versionBytes)
            {
                data["version"] = Encoding.UTF8.GetString(versionBytes);
            }
        }

        return data;
    }
}
