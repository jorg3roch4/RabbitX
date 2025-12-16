namespace RabbitX.Rpc;

/// <summary>
/// Manages pending RPC requests waiting for responses.
/// </summary>
public interface IRpcPendingRequests : IDisposable
{
    /// <summary>
    /// Registers a new pending RPC request.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="correlationId">The correlation ID for tracking.</param>
    /// <param name="tcs">The TaskCompletionSource to signal on response.</param>
    /// <param name="timeout">The timeout for this request.</param>
    void Register<TResponse>(string correlationId, TaskCompletionSource<TResponse> tcs, TimeSpan timeout)
        where TResponse : class;

    /// <summary>
    /// Attempts to complete a pending request with a response.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the request.</param>
    /// <param name="responseBody">The serialized response body.</param>
    /// <returns>True if the request was found and completed.</returns>
    bool TryComplete(string correlationId, ReadOnlyMemory<byte> responseBody);

    /// <summary>
    /// Attempts to fail a pending request with an exception.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the request.</param>
    /// <param name="exception">The exception to fail with.</param>
    /// <returns>True if the request was found and failed.</returns>
    bool TryFail(string correlationId, Exception exception);

    /// <summary>
    /// Attempts to remove a pending request without completing it.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the request.</param>
    /// <returns>True if the request was found and removed.</returns>
    bool TryRemove(string correlationId);

    /// <summary>
    /// Attempts to get a pending request by correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the request.</param>
    /// <param name="pending">The pending request if found.</param>
    /// <returns>True if the request was found.</returns>
    bool TryGet(string correlationId, out PendingRpcRequest? pending);

    /// <summary>
    /// Gets the current number of pending requests.
    /// </summary>
    int PendingCount { get; }
}
