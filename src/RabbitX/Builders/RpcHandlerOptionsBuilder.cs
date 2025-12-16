using RabbitX.Configuration;

namespace RabbitX.Builders;

/// <summary>
/// Fluent builder for configuring RPC handler options.
/// </summary>
public sealed class RpcHandlerOptionsBuilder
{
    private readonly RpcHandlerOptions _options = new();

    /// <summary>
    /// Sets the queue to consume RPC requests from.
    /// </summary>
    public RpcHandlerOptionsBuilder FromQueue(string queue)
    {
        _options.Queue = queue;
        return this;
    }

    /// <summary>
    /// Binds the queue to an exchange with the specified routing key.
    /// </summary>
    public RpcHandlerOptionsBuilder BindToExchange(string exchange, string routingKey)
    {
        _options.Exchange = exchange;
        _options.RoutingKey = routingKey;
        return this;
    }

    /// <summary>
    /// Sets the prefetch count for this consumer.
    /// </summary>
    public RpcHandlerOptionsBuilder WithPrefetchCount(ushort count)
    {
        _options.PrefetchCount = count;
        return this;
    }

    /// <summary>
    /// Configures whether to declare the queue on startup.
    /// </summary>
    public RpcHandlerOptionsBuilder DeclareQueue(bool declare = true)
    {
        _options.DeclareQueue = declare;
        return this;
    }

    /// <summary>
    /// Sets the queue as non-durable.
    /// </summary>
    public RpcHandlerOptionsBuilder NonDurable()
    {
        _options.Durable = false;
        return this;
    }

    /// <summary>
    /// Builds the RPC handler options.
    /// </summary>
    internal RpcHandlerOptions Build() => _options;
}
