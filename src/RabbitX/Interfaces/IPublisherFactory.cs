namespace RabbitX.Interfaces;

/// <summary>
/// Factory for creating message publishers by name.
/// </summary>
public interface IPublisherFactory
{
    /// <summary>
    /// Creates a basic publisher for the specified configuration name.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish.</typeparam>
    /// <param name="publisherName">The name of the publisher configuration.</param>
    /// <returns>A message publisher instance.</returns>
    /// <exception cref="ArgumentException">If the publisher name is not configured.</exception>
    IMessagePublisher<TMessage> CreatePublisher<TMessage>(string publisherName) where TMessage : class;

    /// <summary>
    /// Creates a reliable publisher with broker confirmations for the specified configuration name.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish.</typeparam>
    /// <param name="publisherName">The name of the publisher configuration.</param>
    /// <returns>A reliable message publisher instance.</returns>
    /// <exception cref="ArgumentException">If the publisher name is not configured.</exception>
    IReliableMessagePublisher<TMessage> CreateReliablePublisher<TMessage>(string publisherName) where TMessage : class;

    /// <summary>
    /// Gets the names of all configured publishers.
    /// </summary>
    /// <returns>Collection of publisher names.</returns>
    IEnumerable<string> GetPublisherNames();

    /// <summary>
    /// Checks if a publisher with the given name is configured.
    /// </summary>
    /// <param name="publisherName">The publisher name to check.</param>
    /// <returns>True if configured, false otherwise.</returns>
    bool HasPublisher(string publisherName);
}
