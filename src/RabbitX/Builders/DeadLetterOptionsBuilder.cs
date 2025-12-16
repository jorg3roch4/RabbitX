using RabbitX.Configuration;

namespace RabbitX.Builders;

/// <summary>
/// Fluent builder for configuring Dead Letter Exchange options.
/// </summary>
public sealed class DeadLetterOptionsBuilder
{
    private readonly DeadLetterOptions _options = new();

    /// <summary>
    /// Sets the dead letter exchange name.
    /// </summary>
    public DeadLetterOptionsBuilder Exchange(string exchange)
    {
        _options.Exchange = exchange;
        return this;
    }

    /// <summary>
    /// Sets the routing key for dead letter messages.
    /// </summary>
    public DeadLetterOptionsBuilder RoutingKey(string routingKey)
    {
        _options.RoutingKey = routingKey;
        return this;
    }

    /// <summary>
    /// Sets the dead letter queue name.
    /// </summary>
    public DeadLetterOptionsBuilder Queue(string queue)
    {
        _options.Queue = queue;
        return this;
    }

    /// <summary>
    /// Sets the exchange type for the dead letter exchange.
    /// </summary>
    public DeadLetterOptionsBuilder WithExchangeType(string type)
    {
        _options.ExchangeType = type;
        return this;
    }

    /// <summary>
    /// Makes the dead letter exchange durable.
    /// </summary>
    public DeadLetterOptionsBuilder Durable(bool durable = true)
    {
        _options.Durable = durable;
        return this;
    }

    /// <summary>
    /// Builds the dead letter options.
    /// </summary>
    internal DeadLetterOptions Build() => _options;
}
