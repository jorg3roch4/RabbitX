namespace RabbitX.Rpc;

/// <summary>
/// Handler for processing RPC requests and generating responses.
/// </summary>
/// <typeparam name="TRequest">The request message type.</typeparam>
/// <typeparam name="TResponse">The response message type.</typeparam>
public interface IRpcHandler<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    /// <summary>
    /// Handles an RPC request and returns a response.
    /// The response is automatically sent back to the caller.
    /// </summary>
    /// <param name="context">The RPC context containing the request and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response to send back to the caller.</returns>
    Task<TResponse> HandleAsync(RpcContext<TRequest> context, CancellationToken cancellationToken = default);
}
