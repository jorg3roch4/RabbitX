using RabbitX.Models;

namespace RabbitX.Interfaces;

/// <summary>
/// Reliable message publisher with broker confirmations.
/// Extends IMessagePublisher with confirmation support.
/// </summary>
/// <typeparam name="TMessage">The type of message to publish.</typeparam>
public interface IReliableMessagePublisher<in TMessage> : IMessagePublisher<TMessage>
    where TMessage : class
{
    /// <summary>
    /// Publishes a message and waits for broker confirmation.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    Task<PublishResult> PublishWithConfirmAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message with custom options and waits for broker confirmation.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="options">Publishing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    Task<PublishResult> PublishWithConfirmAsync(TMessage message, PublishOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple messages as a batch with confirmation.
    /// </summary>
    /// <param name="messages">The messages to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each message in the batch.</returns>
    Task<IReadOnlyList<PublishResult>> PublishBatchWithConfirmAsync(
        IEnumerable<TMessage> messages,
        CancellationToken cancellationToken = default);
}
