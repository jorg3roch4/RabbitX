using RabbitMQ.Client;

namespace RabbitX.Connection;

/// <summary>
/// Manages the RabbitMQ connection lifecycle.
/// </summary>
public interface IRabbitMQConnection : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the connection is currently open.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the underlying connection (creates if not exists).
    /// </summary>
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new channel from the connection.
    /// </summary>
    /// <param name="enablePublisherConfirms">Whether to enable publisher confirmations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IChannel> CreateChannelAsync(
        bool enablePublisherConfirms = false,
        CancellationToken cancellationToken = default);
}
