namespace RabbitX.Configuration;

/// <summary>
/// Configuration options for a message consumer.
/// </summary>
public sealed class ConsumerOptions
{
    /// <summary>
    /// The queue to consume messages from.
    /// </summary>
    public string Queue { get; set; } = string.Empty;

    /// <summary>
    /// The exchange to bind the queue to.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// The type of exchange (direct, fanout, topic, headers).
    /// </summary>
    public string ExchangeType { get; set; } = "direct";

    /// <summary>
    /// The routing key for queue binding.
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the queue should survive broker restarts.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Whether the queue is exclusive to this connection.
    /// </summary>
    public bool Exclusive { get; set; } = false;

    /// <summary>
    /// Whether the queue should be deleted when no longer in use.
    /// </summary>
    public bool AutoDelete { get; set; } = false;

    /// <summary>
    /// Quality of Service settings.
    /// </summary>
    public QosOptions Qos { get; set; } = new();

    /// <summary>
    /// Number of messages to prefetch. Shorthand for Qos.PrefetchCount.
    /// </summary>
    public ushort PrefetchCount
    {
        get => Qos.PrefetchCount;
        set => Qos.PrefetchCount = value;
    }

    /// <summary>
    /// Retry policy configuration.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Dead letter exchange configuration.
    /// </summary>
    public DeadLetterOptions? DeadLetter { get; set; }

    /// <summary>
    /// Message time-to-live in seconds. Null means no TTL.
    /// </summary>
    public int? MessageTtlSeconds { get; set; }

    /// <summary>
    /// Additional arguments for queue declaration.
    /// </summary>
    public IDictionary<string, object?>? Arguments { get; set; }
}
