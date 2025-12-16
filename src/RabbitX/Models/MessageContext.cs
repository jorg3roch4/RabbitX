namespace RabbitX.Models;

/// <summary>
/// Context containing the message and its metadata for handler processing.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
public sealed class MessageContext<TMessage> where TMessage : class
{
    /// <summary>
    /// The deserialized message.
    /// </summary>
    public required TMessage Message { get; init; }

    /// <summary>
    /// The unique identifier of the message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The correlation ID for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The timestamp when the message was published.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// The current retry attempt number (0 for first attempt).
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// The exchange the message was published to.
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// The routing key used for the message.
    /// </summary>
    public string? RoutingKey { get; init; }

    /// <summary>
    /// The queue the message was consumed from.
    /// </summary>
    public string? Queue { get; init; }

    /// <summary>
    /// The consumer tag that received this message.
    /// </summary>
    public string? ConsumerTag { get; init; }

    /// <summary>
    /// Custom headers attached to the message.
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// The delivery tag for manual acknowledgment (internal use).
    /// </summary>
    internal ulong DeliveryTag { get; init; }

    /// <summary>
    /// Indicates if this is a redelivered message.
    /// </summary>
    public bool Redelivered { get; init; }

    /// <summary>
    /// Gets a header value by key.
    /// </summary>
    /// <typeparam name="T">The expected type of the header value.</typeparam>
    /// <param name="key">The header key.</param>
    /// <param name="defaultValue">Default value if header not found.</param>
    /// <returns>The header value or default.</returns>
    public T? GetHeader<T>(string key, T? defaultValue = default)
    {
        if (Headers.TryGetValue(key, out var value))
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
}
