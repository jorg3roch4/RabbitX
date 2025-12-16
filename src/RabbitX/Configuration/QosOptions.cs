namespace RabbitX.Configuration;

/// <summary>
/// Quality of Service (QoS) settings for consumers.
/// Controls how many messages are delivered to the consumer at once.
/// </summary>
public sealed class QosOptions
{
    /// <summary>
    /// The maximum size of the message body in bytes (0 = unlimited).
    /// </summary>
    public uint PrefetchSize { get; set; } = 0;

    /// <summary>
    /// The maximum number of unacknowledged messages delivered at once.
    /// Lower values = less throughput but more even distribution.
    /// Higher values = more throughput but potential memory issues.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 1;

    /// <summary>
    /// If true, applies QoS to the entire channel; if false, applies per consumer.
    /// </summary>
    public bool Global { get; set; } = false;
}
