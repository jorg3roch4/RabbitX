using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitX.Configuration;

namespace RabbitX.Connection;

/// <summary>
/// Thread-safe RabbitMQ connection manager with automatic recovery.
/// </summary>
public sealed class RabbitMQConnection : IRabbitMQConnection
{
    private readonly RabbitXOptions _options;
    private readonly ILogger<RabbitMQConnection> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMQConnection"/> class.
    /// </summary>
    /// <param name="options">The RabbitX configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public RabbitMQConnection(RabbitXOptions options, ILogger<RabbitMQConnection> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConnected => _connection?.IsOpen == true;

    /// <inheritdoc />
    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RabbitMQConnection));

        // Fast path: connection exists and is open
        if (_connection is { IsOpen: true })
            return _connection;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_connection is { IsOpen: true })
                return _connection;

            // Close existing broken connection
            if (_connection is not null)
            {
                try
                {
                    await _connection.CloseAsync(cancellationToken);
                    _connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing existing connection");
                }
            }

            // Create new connection
            _connection = await CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IChannel> CreateChannelAsync(
        bool enablePublisherConfirms = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        if (enablePublisherConfirms)
        {
            var options = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            return await connection.CreateChannelAsync(options, cancellationToken);
        }

        return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    private async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var connOptions = _options.Connection;

        var factory = new ConnectionFactory
        {
            HostName = connOptions.HostName,
            Port = connOptions.Port,
            UserName = connOptions.UserName,
            Password = connOptions.Password,
            VirtualHost = connOptions.VirtualHost,
            AutomaticRecoveryEnabled = connOptions.AutomaticRecoveryEnabled,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(connOptions.NetworkRecoveryIntervalSeconds),
            RequestedHeartbeat = TimeSpan.FromSeconds(connOptions.RequestedHeartbeatSeconds),
            ClientProvidedName = connOptions.ClientProvidedName ?? $"RabbitX-{Environment.MachineName}"
        };

        _logger.LogInformation(
            "Connecting to RabbitMQ at {Host}:{Port}/{VHost}",
            connOptions.HostName,
            connOptions.Port,
            connOptions.VirtualHost);

        try
        {
            var connection = await factory.CreateConnectionAsync(cancellationToken);

            connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
            connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
            connection.ConnectionUnblockedAsync += OnConnectionUnblockedAsync;

            _logger.LogInformation(
                "Connected to RabbitMQ. ClientName: {ClientName}",
                connection.ClientProvidedName);

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to connect to RabbitMQ at {Host}:{Port}",
                connOptions.HostName,
                connOptions.Port);
            throw;
        }
    }

    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs args)
    {
        if (args.Initiator == ShutdownInitiator.Application)
        {
            _logger.LogInformation("RabbitMQ connection closed by application");
        }
        else
        {
            _logger.LogWarning(
                "RabbitMQ connection shutdown. Initiator: {Initiator}, ReplyCode: {Code}, ReplyText: {Text}",
                args.Initiator,
                args.ReplyCode,
                args.ReplyText);
        }

        return Task.CompletedTask;
    }

    private Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs args)
    {
        _logger.LogWarning("RabbitMQ connection blocked. Reason: {Reason}", args.Reason);
        return Task.CompletedTask;
    }

    private Task OnConnectionUnblockedAsync(object sender, AsyncEventArgs args)
    {
        _logger.LogInformation("RabbitMQ connection unblocked");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync();
                _connection.Dispose();
                _logger.LogInformation("RabbitMQ connection disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing RabbitMQ connection");
            }
        }

        _connectionLock.Dispose();
    }
}
