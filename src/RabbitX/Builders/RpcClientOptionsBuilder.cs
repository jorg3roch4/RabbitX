using RabbitX.Configuration;

namespace RabbitX.Builders;

/// <summary>
/// Fluent builder for configuring RPC client options.
/// </summary>
public sealed class RpcClientOptionsBuilder
{
    private readonly RpcClientOptions _options = new();

    /// <summary>
    /// Sets the exchange to publish RPC requests to.
    /// </summary>
    /// <param name="exchange">The exchange name.</param>
    /// <param name="exchangeType">The exchange type (default: direct).</param>
    public RpcClientOptionsBuilder ToExchange(string exchange, string exchangeType = "direct")
    {
        _options.Exchange = exchange;
        _options.ExchangeType = exchangeType;
        return this;
    }

    /// <summary>
    /// Sets the routing key for RPC requests.
    /// </summary>
    public RpcClientOptionsBuilder WithRoutingKey(string routingKey)
    {
        _options.RoutingKey = routingKey;
        return this;
    }

    /// <summary>
    /// Sets the default timeout for RPC calls.
    /// </summary>
    public RpcClientOptionsBuilder WithTimeout(TimeSpan timeout)
    {
        _options.TimeoutSeconds = (int)timeout.TotalSeconds;
        return this;
    }

    /// <summary>
    /// Sets the default timeout for RPC calls in seconds.
    /// </summary>
    public RpcClientOptionsBuilder WithTimeout(int seconds)
    {
        _options.TimeoutSeconds = seconds;
        return this;
    }

    /// <summary>
    /// Enables or disables Direct Reply-to feature.
    /// Direct Reply-to provides better performance by avoiding queue declaration.
    /// </summary>
    public RpcClientOptionsBuilder UseDirectReplyTo(bool use = true)
    {
        _options.UseDirectReplyTo = use;
        return this;
    }

    /// <summary>
    /// Sets a custom reply queue prefix (disables Direct Reply-to).
    /// The actual queue name will be {prefix}.{clientId}.
    /// </summary>
    public RpcClientOptionsBuilder WithReplyQueuePrefix(string prefix)
    {
        _options.ReplyQueuePrefix = prefix;
        _options.UseDirectReplyTo = false;
        return this;
    }

    /// <summary>
    /// Configures whether to declare the exchange on first use.
    /// </summary>
    public RpcClientOptionsBuilder DeclareExchange(bool declare = true)
    {
        _options.DeclareExchange = declare;
        return this;
    }

    /// <summary>
    /// Sets the exchange as non-durable.
    /// </summary>
    public RpcClientOptionsBuilder NonDurable()
    {
        _options.Durable = false;
        return this;
    }

    /// <summary>
    /// Builds the RPC client options.
    /// </summary>
    internal RpcClientOptions Build() => _options;
}
