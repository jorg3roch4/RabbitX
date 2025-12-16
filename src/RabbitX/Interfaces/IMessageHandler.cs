using RabbitX.Models;

namespace RabbitX.Interfaces;

/// <summary>
/// Handler interface for processing consumed messages.
/// Implement this interface to define your message processing logic.
/// </summary>
/// <typeparam name="TMessage">The type of message to handle.</typeparam>
public interface IMessageHandler<TMessage> where TMessage : class
{
    /// <summary>
    /// Handles a consumed message.
    /// </summary>
    /// <param name="context">The message context containing the message and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The result indicating how to acknowledge the message:
    /// - Ack: Message processed successfully, remove from queue.
    /// - Retry: Transient failure, retry with configured policy.
    /// - Reject: Permanent failure, send to dead letter queue.
    /// - Defer: Requeue for another consumer to process.
    /// </returns>
    Task<ConsumeResult> HandleAsync(MessageContext<TMessage> context, CancellationToken cancellationToken = default);
}
