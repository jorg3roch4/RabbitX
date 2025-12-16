using Microsoft.Extensions.Logging;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.Interfaces;
using RabbitX.Resilience;

namespace RabbitX.Consumers;

/// <summary>
/// Factory for creating RabbitMQ consumers.
/// </summary>
internal sealed class RabbitMQConsumerFactory : IConsumerFactory
{
    private readonly IRabbitMQConnection _connection;
    private readonly IMessageSerializer _serializer;
    private readonly IRetryPolicyProvider _retryPolicyProvider;
    private readonly RabbitXOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public RabbitMQConsumerFactory(
        IRabbitMQConnection connection,
        IMessageSerializer serializer,
        IRetryPolicyProvider retryPolicyProvider,
        RabbitXOptions options,
        ILoggerFactory loggerFactory)
    {
        _connection = connection;
        _serializer = serializer;
        _retryPolicyProvider = retryPolicyProvider;
        _options = options;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IMessageConsumer<TMessage> CreateConsumer<TMessage>(
        string consumerName,
        IMessageHandler<TMessage> handler)
        where TMessage : class
    {
        if (!_options.Consumers.TryGetValue(consumerName, out var consumerOptions))
        {
            throw new ArgumentException(
                $"Consumer '{consumerName}' is not configured. Available consumers: {string.Join(", ", _options.Consumers.Keys)}",
                nameof(consumerName));
        }

        var logger = _loggerFactory.CreateLogger<RabbitMQConsumer<TMessage>>();

        return new RabbitMQConsumer<TMessage>(
            _connection,
            _serializer,
            handler,
            _retryPolicyProvider,
            consumerOptions,
            consumerName,
            logger);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetConsumerNames() => _options.Consumers.Keys;

    /// <inheritdoc />
    public bool HasConsumer(string consumerName) => _options.Consumers.ContainsKey(consumerName);
}
