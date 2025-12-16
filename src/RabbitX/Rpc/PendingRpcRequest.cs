namespace RabbitX.Rpc;

/// <summary>
/// Represents a pending RPC request waiting for a response.
/// </summary>
public sealed class PendingRpcRequest
{
    /// <summary>
    /// The expected response type.
    /// </summary>
    public Type ResponseType { get; }

    /// <summary>
    /// The TaskCompletionSource to signal when the response arrives.
    /// </summary>
    public object TaskCompletionSource { get; }

    /// <summary>
    /// When this request was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// The timeout duration for this request.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Creates a new pending RPC request.
    /// </summary>
    public PendingRpcRequest(Type responseType, object tcs, TimeSpan timeout)
    {
        ResponseType = responseType;
        TaskCompletionSource = tcs;
        CreatedAt = DateTimeOffset.UtcNow;
        Timeout = timeout;
    }

    /// <summary>
    /// Indicates whether this request has expired based on its timeout.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow - CreatedAt > Timeout;

    /// <summary>
    /// Completes the request with a successful response.
    /// </summary>
    /// <param name="response">The response object.</param>
    public void Complete(object response)
    {
        var setResultMethod = TaskCompletionSource.GetType().GetMethod("TrySetResult");
        setResultMethod?.Invoke(TaskCompletionSource, [response]);
    }

    /// <summary>
    /// Fails the request with an exception.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    public void Fail(Exception exception)
    {
        var setExceptionMethod = TaskCompletionSource.GetType().GetMethod("TrySetException", [typeof(Exception)]);
        setExceptionMethod?.Invoke(TaskCompletionSource, [exception]);
    }

    /// <summary>
    /// Cancels the request.
    /// </summary>
    public void Cancel()
    {
        var setCanceledMethod = TaskCompletionSource.GetType().GetMethod("TrySetCanceled", Type.EmptyTypes);
        setCanceledMethod?.Invoke(TaskCompletionSource, null);
    }
}
