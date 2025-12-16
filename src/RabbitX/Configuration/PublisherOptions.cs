namespace RabbitX.Configuration;

/// <summary>
/// Configuration options for a message publisher.
/// </summary>
public sealed class PublisherOptions
{
    /// <summary>
    /// The exchange to publish messages to.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// The type of exchange (direct, fanout, topic, headers).
    /// </summary>
    public string ExchangeType { get; set; } = "direct";

    /// <summary>
    /// The routing key for message routing.
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the exchange should survive broker restarts.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Whether the exchange should be deleted when no longer in use.
    /// </summary>
    public bool AutoDelete { get; set; } = false;

    /// <summary>
    /// Whether messages should be persisted to disk.
    /// </summary>
    public bool Persistent { get; set; } = true;

    /// <summary>
    /// Whether to return unroutable messages to the publisher.
    /// </summary>
    public bool Mandatory { get; set; } = false;

    /// <summary>
    /// Timeout for publish confirmations in milliseconds.
    /// </summary>
    public int ConfirmTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Additional arguments for exchange declaration.
    /// </summary>
    public IDictionary<string, object?>? Arguments { get; set; }
}
