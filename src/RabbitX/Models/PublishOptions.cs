namespace RabbitX.Models;

/// <summary>
/// Options for customizing message publishing.
/// </summary>
public sealed class PublishOptions
{
    /// <summary>
    /// Correlation ID for tracking related messages across services.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Message expiration time. Message will be discarded if not consumed within this time.
    /// </summary>
    public TimeSpan? Expiration { get; init; }

    /// <summary>
    /// Message priority (0-9, where 9 is highest priority).
    /// Requires the queue to be configured with x-max-priority.
    /// </summary>
    public byte? Priority { get; init; }

    /// <summary>
    /// Custom headers to attach to the message.
    /// </summary>
    public Dictionary<string, object>? Headers { get; init; }

    /// <summary>
    /// Override the routing key configured for the publisher.
    /// </summary>
    public string? RoutingKeyOverride { get; init; }

    /// <summary>
    /// The message type identifier (defaults to the type name).
    /// </summary>
    public string? MessageType { get; init; }

    /// <summary>
    /// Application-specific ID for the message.
    /// </summary>
    public string? AppId { get; init; }

    /// <summary>
    /// Creates default publish options.
    /// </summary>
    public static PublishOptions Default => new();

    /// <summary>
    /// Creates publish options with a correlation ID.
    /// </summary>
    public static PublishOptions WithCorrelation(string correlationId) =>
        new() { CorrelationId = correlationId };

    /// <summary>
    /// Creates publish options with custom headers.
    /// </summary>
    public static PublishOptions WithHeaders(Dictionary<string, object> headers) =>
        new() { Headers = headers };
}
