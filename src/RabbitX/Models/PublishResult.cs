namespace RabbitX.Models;

/// <summary>
/// Result of a publish operation with broker confirmation.
/// </summary>
public sealed record PublishResult
{
    /// <summary>
    /// Indicates whether the message was successfully confirmed by the broker.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The unique identifier assigned to the message.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// The correlation ID if one was provided.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Error message if the publish failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Creates a successful publish result.
    /// </summary>
    public static PublishResult Confirmed(string messageId, string? correlationId = null) =>
        new()
        {
            Success = true,
            MessageId = messageId,
            CorrelationId = correlationId
        };

    /// <summary>
    /// Creates a failed publish result.
    /// </summary>
    public static PublishResult Failed(string errorMessage, Exception? exception = null) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };

    /// <summary>
    /// Creates a failed publish result from an exception.
    /// </summary>
    public static PublishResult FromException(Exception exception) =>
        new()
        {
            Success = false,
            ErrorMessage = exception.Message,
            Exception = exception
        };
}
