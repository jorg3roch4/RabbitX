namespace RabbitX.Diagnostics;

/// <summary>
/// Provides OpenTelemetry metrics instruments for RabbitX operations.
/// When no MeterListener is configured, instrument operations are no-ops.
/// </summary>
internal static class RabbitXMeter
{
    private static readonly Meter Meter = new(
        RabbitXTelemetryConstants.MeterName,
        typeof(RabbitXMeter).Assembly.GetName().Version?.ToString() ?? "1.0.0");

    // --- Publishing metrics ---

    /// <summary>Total messages published successfully.</summary>
    public static readonly Counter<long> MessagesPublished =
        Meter.CreateCounter<long>("rabbitx.messages.published", "messages", "Total messages published successfully");

    /// <summary>Total publish errors.</summary>
    public static readonly Counter<long> PublishErrors =
        Meter.CreateCounter<long>("rabbitx.messages.publish.errors", "errors", "Total publish errors");

    /// <summary>Publish operation duration in milliseconds.</summary>
    public static readonly Histogram<double> PublishDuration =
        Meter.CreateHistogram<double>("rabbitx.messages.publish.duration", "ms", "Publish operation duration");

    /// <summary>Published message body size in bytes.</summary>
    public static readonly Histogram<long> PublishMessageSize =
        Meter.CreateHistogram<long>("rabbitx.messages.publish.size", "bytes", "Published message body size");

    // --- Consuming metrics ---

    /// <summary>Total messages received for processing.</summary>
    public static readonly Counter<long> MessagesConsumed =
        Meter.CreateCounter<long>("rabbitx.messages.consumed", "messages", "Total messages received for processing");

    /// <summary>Consume results by result type (ack, retry, reject, defer).</summary>
    public static readonly Counter<long> ConsumeResults =
        Meter.CreateCounter<long>("rabbitx.messages.consume.results", "messages", "Consume results by type");

    /// <summary>Message processing duration in milliseconds.</summary>
    public static readonly Histogram<double> ConsumeDuration =
        Meter.CreateHistogram<double>("rabbitx.messages.consume.duration", "ms", "Message processing duration");

    /// <summary>Total unhandled consume errors.</summary>
    public static readonly Counter<long> ConsumeErrors =
        Meter.CreateCounter<long>("rabbitx.messages.consume.errors", "errors", "Total unhandled consume errors");

    // --- Retry metrics ---

    /// <summary>Total retry attempts.</summary>
    public static readonly Counter<long> RetryAttempts =
        Meter.CreateCounter<long>("rabbitx.messages.retries", "retries", "Total retry attempts");

    /// <summary>Total retry exhaustions.</summary>
    public static readonly Counter<long> RetryExhausted =
        Meter.CreateCounter<long>("rabbitx.messages.retries.exhausted", "messages", "Total retry exhaustions");

    // --- RPC metrics ---

    /// <summary>Total RPC calls made.</summary>
    public static readonly Counter<long> RpcCallsMade =
        Meter.CreateCounter<long>("rabbitx.rpc.calls", "calls", "Total RPC calls made");

    /// <summary>RPC call duration in milliseconds.</summary>
    public static readonly Histogram<double> RpcDuration =
        Meter.CreateHistogram<double>("rabbitx.rpc.duration", "ms", "RPC call duration");

    /// <summary>Total RPC timeouts.</summary>
    public static readonly Counter<long> RpcTimeouts =
        Meter.CreateCounter<long>("rabbitx.rpc.timeouts", "timeouts", "Total RPC timeouts");

    /// <summary>Total RPC errors.</summary>
    public static readonly Counter<long> RpcErrors =
        Meter.CreateCounter<long>("rabbitx.rpc.errors", "errors", "Total RPC errors");

    // --- Connection metrics ---

    /// <summary>Total connections created.</summary>
    public static readonly Counter<long> ConnectionsCreated =
        Meter.CreateCounter<long>("rabbitx.connections.created", "connections", "Total connections created");

    /// <summary>Total connection errors.</summary>
    public static readonly Counter<long> ConnectionErrors =
        Meter.CreateCounter<long>("rabbitx.connections.errors", "errors", "Total connection errors");
}
