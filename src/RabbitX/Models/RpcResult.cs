namespace RabbitX.Models;

/// <summary>
/// Result of an RPC call operation.
/// </summary>
/// <typeparam name="TResponse">The type of the expected response.</typeparam>
public sealed record RpcResult<TResponse> where TResponse : class
{
    /// <summary>
    /// Indicates whether the RPC call was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The response from the RPC handler. Null if the call failed.
    /// </summary>
    public TResponse? Response { get; init; }

    /// <summary>
    /// The correlation ID used for this RPC call.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The total duration of the RPC call.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if the call failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Indicates whether the failure was due to a timeout.
    /// </summary>
    public bool IsTimeout => ErrorMessage?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Creates a successful RPC result.
    /// </summary>
    public static RpcResult<TResponse> Ok(TResponse response, string correlationId, TimeSpan duration)
        => new()
        {
            Success = true,
            Response = response,
            CorrelationId = correlationId,
            Duration = duration
        };

    /// <summary>
    /// Creates a timeout result.
    /// </summary>
    public static RpcResult<TResponse> Timeout(string correlationId, TimeSpan timeout)
        => new()
        {
            Success = false,
            CorrelationId = correlationId,
            Duration = timeout,
            ErrorMessage = $"RPC timeout after {timeout.TotalSeconds}s"
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static RpcResult<TResponse> Failed(string correlationId, string error, Exception? exception = null)
        => new()
        {
            Success = false,
            CorrelationId = correlationId,
            ErrorMessage = error,
            Exception = exception
        };

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static RpcResult<TResponse> FromException(string correlationId, Exception exception)
        => new()
        {
            Success = false,
            CorrelationId = correlationId,
            ErrorMessage = exception.Message,
            Exception = exception
        };
}
