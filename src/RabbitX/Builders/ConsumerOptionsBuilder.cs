using RabbitX.Configuration;

namespace RabbitX.Builders;

/// <summary>
/// Fluent builder for configuring consumer options.
/// </summary>
public sealed class ConsumerOptionsBuilder
{
    private readonly ConsumerOptions _options;

    /// <summary>
    /// Creates a new builder with default options.
    /// </summary>
    public ConsumerOptionsBuilder()
    {
        _options = new ConsumerOptions();
    }

    /// <summary>
    /// Creates a builder from existing options (for modification).
    /// </summary>
    internal ConsumerOptionsBuilder(ConsumerOptions existing)
    {
        _options = new ConsumerOptions
        {
            Queue = existing.Queue,
            Exchange = existing.Exchange,
            ExchangeType = existing.ExchangeType,
            RoutingKey = existing.RoutingKey,
            Durable = existing.Durable,
            Exclusive = existing.Exclusive,
            AutoDelete = existing.AutoDelete,
            Qos = new QosOptions
            {
                PrefetchSize = existing.Qos.PrefetchSize,
                PrefetchCount = existing.Qos.PrefetchCount,
                Global = existing.Qos.Global
            },
            Retry = new RetryOptions
            {
                MaxRetries = existing.Retry.MaxRetries,
                DelaysInSeconds = existing.Retry.DelaysInSeconds?.ToArray(),
                InitialDelaySeconds = existing.Retry.InitialDelaySeconds,
                MaxDelaySeconds = existing.Retry.MaxDelaySeconds,
                BackoffMultiplier = existing.Retry.BackoffMultiplier,
                UseJitter = existing.Retry.UseJitter,
                OnRetryExhausted = existing.Retry.OnRetryExhausted
            },
            DeadLetter = existing.DeadLetter != null
                ? new DeadLetterOptions
                {
                    Exchange = existing.DeadLetter.Exchange,
                    RoutingKey = existing.DeadLetter.RoutingKey,
                    Queue = existing.DeadLetter.Queue,
                    ExchangeType = existing.DeadLetter.ExchangeType,
                    Durable = existing.DeadLetter.Durable
                }
                : null,
            MessageTtlSeconds = existing.MessageTtlSeconds,
            Arguments = existing.Arguments != null
                ? new Dictionary<string, object?>(existing.Arguments)
                : null
        };
    }

    /// <summary>
    /// Configures the queue to consume from.
    /// </summary>
    public ConsumerOptionsBuilder FromQueue(string queue)
    {
        _options.Queue = queue;
        return this;
    }

    /// <summary>
    /// Binds the queue to an exchange with a routing key.
    /// </summary>
    public ConsumerOptionsBuilder BindToExchange(string exchange, string routingKey, string type = "direct")
    {
        _options.Exchange = exchange;
        _options.RoutingKey = routingKey;
        _options.ExchangeType = type;
        return this;
    }

    /// <summary>
    /// Makes the queue durable (survives broker restart).
    /// </summary>
    public ConsumerOptionsBuilder Durable(bool durable = true)
    {
        _options.Durable = durable;
        return this;
    }

    /// <summary>
    /// Makes the queue exclusive to this connection.
    /// </summary>
    public ConsumerOptionsBuilder Exclusive(bool exclusive = true)
    {
        _options.Exclusive = exclusive;
        return this;
    }

    /// <summary>
    /// Configures the queue to auto-delete when no longer used.
    /// </summary>
    public ConsumerOptionsBuilder AutoDelete(bool autoDelete = true)
    {
        _options.AutoDelete = autoDelete;
        return this;
    }

    /// <summary>
    /// Sets the prefetch count (messages delivered before ack).
    /// </summary>
    public ConsumerOptionsBuilder WithPrefetchCount(ushort count)
    {
        _options.Qos.PrefetchCount = count;
        return this;
    }

    /// <summary>
    /// Sets the prefetch count (messages delivered before ack).
    /// Alias for WithPrefetchCount.
    /// </summary>
    public ConsumerOptionsBuilder WithPrefetch(ushort count) => WithPrefetchCount(count);

    /// <summary>
    /// Sets the prefetch size in bytes (0 = unlimited).
    /// </summary>
    public ConsumerOptionsBuilder WithPrefetchSize(uint size)
    {
        _options.Qos.PrefetchSize = size;
        return this;
    }

    /// <summary>
    /// Sets whether QoS applies globally to the channel or per consumer.
    /// </summary>
    public ConsumerOptionsBuilder WithGlobalQos(bool global = true)
    {
        _options.Qos.Global = global;
        return this;
    }

    /// <summary>
    /// Configures Quality of Service settings.
    /// </summary>
    public ConsumerOptionsBuilder WithQos(Action<QosOptions> configure)
    {
        configure(_options.Qos);
        return this;
    }

    /// <summary>
    /// Configures retry policy using a builder.
    /// </summary>
    public ConsumerOptionsBuilder WithRetry(Action<RetryOptionsBuilder> configure)
    {
        var builder = new RetryOptionsBuilder(_options.Retry);
        configure(builder);
        _options.Retry = builder.Build();
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retries.
    /// </summary>
    public ConsumerOptionsBuilder WithMaxRetries(int maxRetries)
    {
        _options.Retry.MaxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Configures Dead Letter Exchange using a builder.
    /// </summary>
    public ConsumerOptionsBuilder WithDeadLetter(Action<DeadLetterOptionsBuilder> configure)
    {
        var builder = new DeadLetterOptionsBuilder();
        configure(builder);
        _options.DeadLetter = builder.Build();
        return this;
    }

    /// <summary>
    /// Configures Dead Letter Exchange with simple parameters.
    /// </summary>
    public ConsumerOptionsBuilder WithDeadLetter(string exchange, string routingKey, string queue)
    {
        _options.DeadLetter = new DeadLetterOptions
        {
            Exchange = exchange,
            RoutingKey = routingKey,
            Queue = queue
        };
        return this;
    }

    /// <summary>
    /// Sets the message time-to-live.
    /// </summary>
    public ConsumerOptionsBuilder WithMessageTtl(TimeSpan ttl)
    {
        _options.MessageTtlSeconds = (int)ttl.TotalSeconds;
        return this;
    }

    /// <summary>
    /// Adds custom arguments for queue declaration.
    /// </summary>
    public ConsumerOptionsBuilder WithArguments(IDictionary<string, object?> arguments)
    {
        _options.Arguments = arguments;
        return this;
    }

    /// <summary>
    /// Adds a single custom argument.
    /// </summary>
    public ConsumerOptionsBuilder WithArgument(string key, object? value)
    {
        _options.Arguments ??= new Dictionary<string, object?>();
        _options.Arguments[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the consumer options.
    /// </summary>
    internal ConsumerOptions Build() => _options;
}
