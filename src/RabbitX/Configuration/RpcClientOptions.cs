namespace RabbitX.Configuration;

/// <summary>
/// Configuration options for an RPC client.
/// </summary>
public sealed class RpcClientOptions
{
    /// <summary>
    /// The name of this RPC client configuration.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The exchange to publish RPC requests to.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// The exchange type (direct, topic, fanout, headers).
    /// </summary>
    public string ExchangeType { get; set; } = "direct";

    /// <summary>
    /// The routing key for RPC requests.
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Default timeout for RPC calls in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to use RabbitMQ's Direct Reply-to feature (recommended).
    /// When true, uses amq.rabbitmq.reply-to pseudo-queue for better performance.
    /// </summary>
    public bool UseDirectReplyTo { get; set; } = true;

    /// <summary>
    /// Prefix for custom reply queue names (when UseDirectReplyTo is false).
    /// </summary>
    public string? ReplyQueuePrefix { get; set; }

    /// <summary>
    /// Whether to declare the exchange on first use.
    /// </summary>
    public bool DeclareExchange { get; set; } = true;

    /// <summary>
    /// Whether the exchange is durable.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// The request message type (set by fluent API).
    /// </summary>
    public Type? RequestType { get; internal set; }

    /// <summary>
    /// The response message type (set by fluent API).
    /// </summary>
    public Type? ResponseType { get; internal set; }

    /// <summary>
    /// Gets the timeout as a TimeSpan.
    /// </summary>
    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
}
