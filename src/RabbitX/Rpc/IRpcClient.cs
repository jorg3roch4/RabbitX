using RabbitX.Models;

namespace RabbitX.Rpc;

/// <summary>
/// Client for making RPC (Request-Reply) calls over RabbitMQ.
/// Provides synchronous-style calls over asynchronous messaging.
/// </summary>
/// <typeparam name="TRequest">The request message type.</typeparam>
/// <typeparam name="TResponse">The response message type.</typeparam>
public interface IRpcClient<TRequest, TResponse> : IAsyncDisposable
    where TRequest : class
    where TResponse : class
{
    /// <summary>
    /// Calls the RPC endpoint and waits for a response.
    /// Throws TimeoutException if no response is received within the configured timeout.
    /// </summary>
    /// <param name="request">The request message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the RPC handler.</returns>
    /// <exception cref="TimeoutException">Thrown when the RPC times out.</exception>
    /// <exception cref="InvalidOperationException">Thrown on RPC failure.</exception>
    Task<TResponse> CallAsync(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the RPC endpoint with custom options and waits for a response.
    /// Throws TimeoutException if no response is received within the timeout.
    /// </summary>
    /// <param name="request">The request message.</param>
    /// <param name="options">Options for this specific call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the RPC handler.</returns>
    /// <exception cref="TimeoutException">Thrown when the RPC times out.</exception>
    /// <exception cref="InvalidOperationException">Thrown on RPC failure.</exception>
    Task<TResponse> CallAsync(TRequest request, RpcCallOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the RPC endpoint and returns a result object.
    /// Does not throw on timeout or RPC errors - check RpcResult.Success instead.
    /// </summary>
    /// <param name="request">The request message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An RpcResult containing the response or error information.</returns>
    Task<RpcResult<TResponse>> TryCallAsync(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the RPC endpoint with a custom timeout and returns a result object.
    /// Does not throw on timeout or RPC errors - check RpcResult.Success instead.
    /// </summary>
    /// <param name="request">The request message.</param>
    /// <param name="timeout">Custom timeout for this call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An RpcResult containing the response or error information.</returns>
    Task<RpcResult<TResponse>> TryCallAsync(TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default);
}
