using RabbitX.Configuration;

namespace RabbitX.Builders;

/// <summary>
/// Fluent builder for configuring publisher options.
/// </summary>
public sealed class PublisherOptionsBuilder
{
    private readonly PublisherOptions _options;

    /// <summary>
    /// Creates a new builder with default options.
    /// </summary>
    public PublisherOptionsBuilder()
    {
        _options = new PublisherOptions();
    }

    /// <summary>
    /// Creates a builder from existing options (for modification).
    /// </summary>
    internal PublisherOptionsBuilder(PublisherOptions existing)
    {
        _options = new PublisherOptions
        {
            Exchange = existing.Exchange,
            ExchangeType = existing.ExchangeType,
            RoutingKey = existing.RoutingKey,
            Durable = existing.Durable,
            AutoDelete = existing.AutoDelete,
            Persistent = existing.Persistent,
            Mandatory = existing.Mandatory,
            ConfirmTimeoutMs = existing.ConfirmTimeoutMs,
            Arguments = existing.Arguments != null
                ? new Dictionary<string, object?>(existing.Arguments)
                : null
        };
    }

    /// <summary>
    /// Configures the exchange to publish to.
    /// </summary>
    /// <param name="exchange">The exchange name.</param>
    /// <param name="type">The exchange type (direct, fanout, topic, headers).</param>
    public PublisherOptionsBuilder ToExchange(string exchange, string type = "direct")
    {
        _options.Exchange = exchange;
        _options.ExchangeType = type;
        return this;
    }

    /// <summary>
    /// Configures the routing key for messages.
    /// </summary>
    public PublisherOptionsBuilder WithRoutingKey(string routingKey)
    {
        _options.RoutingKey = routingKey;
        return this;
    }

    /// <summary>
    /// Makes the exchange durable (survives broker restart).
    /// </summary>
    public PublisherOptionsBuilder Durable(bool durable = true)
    {
        _options.Durable = durable;
        return this;
    }

    /// <summary>
    /// Makes messages persistent (written to disk).
    /// </summary>
    public PublisherOptionsBuilder Persistent(bool persistent = true)
    {
        _options.Persistent = persistent;
        return this;
    }

    /// <summary>
    /// Configures the exchange to auto-delete when no longer used.
    /// </summary>
    public PublisherOptionsBuilder AutoDelete(bool autoDelete = true)
    {
        _options.AutoDelete = autoDelete;
        return this;
    }

    /// <summary>
    /// Makes publishing mandatory (returns unroutable messages).
    /// </summary>
    public PublisherOptionsBuilder Mandatory(bool mandatory = true)
    {
        _options.Mandatory = mandatory;
        return this;
    }

    /// <summary>
    /// Sets the timeout for publish confirmations.
    /// </summary>
    public PublisherOptionsBuilder WithConfirmTimeout(TimeSpan timeout)
    {
        _options.ConfirmTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Adds custom arguments for exchange declaration.
    /// </summary>
    public PublisherOptionsBuilder WithArguments(IDictionary<string, object?> arguments)
    {
        _options.Arguments = arguments;
        return this;
    }

    /// <summary>
    /// Adds a single custom argument.
    /// </summary>
    public PublisherOptionsBuilder WithArgument(string key, object? value)
    {
        _options.Arguments ??= new Dictionary<string, object?>();
        _options.Arguments[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the publisher options.
    /// </summary>
    internal PublisherOptions Build() => _options;
}
