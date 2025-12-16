using RabbitX.Models;

namespace RabbitX.Interfaces;

/// <summary>
/// Basic message publisher interface for fire-and-forget publishing.
/// </summary>
/// <typeparam name="TMessage">The type of message to publish.</typeparam>
public interface IMessagePublisher<in TMessage> where TMessage : class
{
    /// <summary>
    /// Publishes a message to the configured exchange.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message with custom options.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="options">Publishing options (correlation ID, headers, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(TMessage message, PublishOptions options, CancellationToken cancellationToken = default);
}
