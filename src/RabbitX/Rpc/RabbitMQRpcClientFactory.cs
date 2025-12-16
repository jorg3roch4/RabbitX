using Microsoft.Extensions.Logging;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.Interfaces;

namespace RabbitX.Rpc;

/// <summary>
/// Factory for creating RabbitMQ RPC clients.
/// </summary>
internal sealed class RabbitMQRpcClientFactory : IRpcClientFactory
{
    private readonly RabbitXOptions _options;
    private readonly IRabbitMQConnection _connection;
    private readonly IRpcPendingRequests _pendingRequests;
    private readonly IMessageSerializer _serializer;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Creates a new RPC client factory.
    /// </summary>
    public RabbitMQRpcClientFactory(
        RabbitXOptions options,
        IRabbitMQConnection connection,
        IRpcPendingRequests pendingRequests,
        IMessageSerializer serializer,
        ILoggerFactory loggerFactory)
    {
        _options = options;
        _connection = connection;
        _pendingRequests = pendingRequests;
        _serializer = serializer;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IRpcClient<TRequest, TResponse> CreateClient<TRequest, TResponse>(string name)
        where TRequest : class
        where TResponse : class
    {
        if (!_options.RpcClients.TryGetValue(name, out var clientOptions))
        {
            throw new InvalidOperationException(
                $"RPC client configuration '{name}' not found. " +
                $"Available configurations: {string.Join(", ", _options.RpcClients.Keys)}");
        }

        // Validate types match if specified
        if (clientOptions.RequestType != null && clientOptions.RequestType != typeof(TRequest))
        {
            throw new InvalidOperationException(
                $"RPC client '{name}' was configured for request type {clientOptions.RequestType.Name}, " +
                $"but CreateClient was called with {typeof(TRequest).Name}");
        }

        if (clientOptions.ResponseType != null && clientOptions.ResponseType != typeof(TResponse))
        {
            throw new InvalidOperationException(
                $"RPC client '{name}' was configured for response type {clientOptions.ResponseType.Name}, " +
                $"but CreateClient was called with {typeof(TResponse).Name}");
        }

        var logger = _loggerFactory.CreateLogger<RabbitMQRpcClient<TRequest, TResponse>>();

        return new RabbitMQRpcClient<TRequest, TResponse>(
            clientOptions,
            _connection,
            _pendingRequests,
            _serializer,
            logger);
    }
}
