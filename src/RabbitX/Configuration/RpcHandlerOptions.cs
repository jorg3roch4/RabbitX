namespace RabbitX.Configuration;

/// <summary>
/// Configuration options for an RPC handler.
/// </summary>
public sealed class RpcHandlerOptions
{
    /// <summary>
    /// The name of this RPC handler configuration.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The queue to consume RPC requests from.
    /// </summary>
    public string Queue { get; set; } = string.Empty;

    /// <summary>
    /// The exchange to bind the queue to (optional).
    /// </summary>
    public string? Exchange { get; set; }

    /// <summary>
    /// The exchange type (direct, fanout, topic, headers).
    /// </summary>
    public string ExchangeType { get; set; } = "direct";

    /// <summary>
    /// The routing key for queue binding (optional).
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// Whether to declare the queue on startup.
    /// </summary>
    public bool DeclareQueue { get; set; } = true;

    /// <summary>
    /// Whether the queue is durable.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// The prefetch count for this consumer.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// The request message type (set by fluent API).
    /// </summary>
    public Type? RequestType { get; internal set; }

    /// <summary>
    /// The response message type (set by fluent API).
    /// </summary>
    public Type? ResponseType { get; internal set; }

    /// <summary>
    /// The handler type (set by fluent API).
    /// </summary>
    public Type? HandlerType { get; internal set; }
}
