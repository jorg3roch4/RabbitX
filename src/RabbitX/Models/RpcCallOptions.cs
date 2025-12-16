namespace RabbitX.Models;

/// <summary>
/// Options for individual RPC calls.
/// </summary>
public sealed record RpcCallOptions
{
    /// <summary>
    /// Custom timeout for this specific call. If null, uses the client's default timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Custom correlation ID for tracking. If null, a GUID is generated.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Additional headers to include with the message.
    /// </summary>
    public Dictionary<string, object>? Headers { get; init; }

    /// <summary>
    /// Default options with no customization.
    /// </summary>
    public static RpcCallOptions Default => new();

    /// <summary>
    /// Creates options with a specific timeout.
    /// </summary>
    public static RpcCallOptions WithTimeout(TimeSpan timeout) => new() { Timeout = timeout };

    /// <summary>
    /// Creates options with a specific correlation ID.
    /// </summary>
    public static RpcCallOptions WithCorrelationId(string correlationId) => new() { CorrelationId = correlationId };
}
