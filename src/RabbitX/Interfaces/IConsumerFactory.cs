namespace RabbitX.Interfaces;

/// <summary>
/// Factory for creating message consumers by name.
/// </summary>
public interface IConsumerFactory
{
    /// <summary>
    /// Creates a consumer for the specified configuration name.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to consume.</typeparam>
    /// <param name="consumerName">The name of the consumer configuration.</param>
    /// <param name="handler">The message handler to process consumed messages.</param>
    /// <returns>A message consumer instance.</returns>
    /// <exception cref="ArgumentException">If the consumer name is not configured.</exception>
    IMessageConsumer<TMessage> CreateConsumer<TMessage>(
        string consumerName,
        IMessageHandler<TMessage> handler) where TMessage : class;

    /// <summary>
    /// Gets the names of all configured consumers.
    /// </summary>
    /// <returns>Collection of consumer names.</returns>
    IEnumerable<string> GetConsumerNames();

    /// <summary>
    /// Checks if a consumer with the given name is configured.
    /// </summary>
    /// <param name="consumerName">The consumer name to check.</param>
    /// <returns>True if configured, false otherwise.</returns>
    bool HasConsumer(string consumerName);
}
