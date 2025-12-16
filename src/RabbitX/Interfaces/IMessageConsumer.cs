namespace RabbitX.Interfaces;

/// <summary>
/// Message consumer that processes messages from a queue.
/// </summary>
/// <typeparam name="TMessage">The type of message to consume.</typeparam>
public interface IMessageConsumer<TMessage> : IAsyncDisposable where TMessage : class
{
    /// <summary>
    /// Gets the name of this consumer (from configuration).
    /// </summary>
    string ConsumerName { get; }

    /// <summary>
    /// Gets whether the consumer is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts consuming messages from the configured queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop consuming.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops consuming messages gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
