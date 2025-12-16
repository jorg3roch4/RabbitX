using Microsoft.Extensions.Logging;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.Interfaces;

namespace RabbitX.Publishers;

/// <summary>
/// Factory for creating RabbitMQ publishers.
/// </summary>
internal sealed class RabbitMQPublisherFactory : IPublisherFactory
{
    private readonly IRabbitMQConnection _connection;
    private readonly IMessageSerializer _serializer;
    private readonly RabbitXOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public RabbitMQPublisherFactory(
        IRabbitMQConnection connection,
        IMessageSerializer serializer,
        RabbitXOptions options,
        ILoggerFactory loggerFactory)
    {
        _connection = connection;
        _serializer = serializer;
        _options = options;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IMessagePublisher<TMessage> CreatePublisher<TMessage>(string publisherName)
        where TMessage : class
    {
        return CreateReliablePublisher<TMessage>(publisherName);
    }

    /// <inheritdoc />
    public IReliableMessagePublisher<TMessage> CreateReliablePublisher<TMessage>(string publisherName)
        where TMessage : class
    {
        if (!_options.Publishers.TryGetValue(publisherName, out var publisherOptions))
        {
            throw new ArgumentException(
                $"Publisher '{publisherName}' is not configured. Available publishers: {string.Join(", ", _options.Publishers.Keys)}",
                nameof(publisherName));
        }

        var logger = _loggerFactory.CreateLogger<RabbitMQPublisher<TMessage>>();

        return new RabbitMQPublisher<TMessage>(
            _connection,
            _serializer,
            publisherOptions,
            publisherName,
            logger);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPublisherNames() => _options.Publishers.Keys;

    /// <inheritdoc />
    public bool HasPublisher(string publisherName) => _options.Publishers.ContainsKey(publisherName);
}
