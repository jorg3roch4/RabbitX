namespace RabbitX.Configuration;

/// <summary>
/// Configuration for Dead Letter Exchange (DLX).
/// Messages that cannot be processed will be routed here.
/// </summary>
public sealed class DeadLetterOptions
{
    /// <summary>
    /// The dead letter exchange name.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// The routing key for dead letter messages.
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// The dead letter queue name.
    /// </summary>
    public string Queue { get; set; } = string.Empty;

    /// <summary>
    /// The type of the dead letter exchange.
    /// </summary>
    public string ExchangeType { get; set; } = "direct";

    /// <summary>
    /// Whether the DLX exchange should be durable.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Validates that the dead letter configuration is complete.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Exchange) &&
        !string.IsNullOrWhiteSpace(Queue);
}
