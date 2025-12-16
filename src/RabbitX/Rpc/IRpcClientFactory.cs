namespace RabbitX.Rpc;

/// <summary>
/// Factory for creating RPC clients.
/// </summary>
public interface IRpcClientFactory
{
    /// <summary>
    /// Creates an RPC client for the specified configuration.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <param name="name">The name of the RPC client configuration.</param>
    /// <returns>An RPC client instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the configuration is not found.</exception>
    IRpcClient<TRequest, TResponse> CreateClient<TRequest, TResponse>(string name)
        where TRequest : class
        where TResponse : class;
}
