namespace RabbitX.Rpc;

/// <summary>
/// Context passed to RPC handlers containing the request message and metadata.
/// </summary>
/// <typeparam name="TRequest">The request message type.</typeparam>
public sealed record RpcContext<TRequest> where TRequest : class
{
    /// <summary>
    /// The request message.
    /// </summary>
    public required TRequest Message { get; init; }

    /// <summary>
    /// The unique message ID.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The correlation ID for tracking this RPC call.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// The reply-to queue where the response should be sent.
    /// </summary>
    public required string ReplyTo { get; init; }

    /// <summary>
    /// Additional headers from the request message.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Headers { get; init; }

    /// <summary>
    /// When the request was received.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
