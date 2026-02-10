using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.Diagnostics;
using RabbitX.Interfaces;
using RabbitX.Models;

namespace RabbitX.Publishers;

/// <summary>
/// RabbitMQ implementation of reliable message publisher with broker confirmations.
/// </summary>
/// <typeparam name="TMessage">The type of message to publish.</typeparam>
internal sealed class RabbitMQPublisher<TMessage> : IReliableMessagePublisher<TMessage>, IAsyncDisposable
    where TMessage : class
{
    private readonly IRabbitMQConnection _connection;
    private readonly IMessageSerializer _serializer;
    private readonly PublisherOptions _options;
    private readonly string _publisherName;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    private IChannel? _channel;
    private bool _exchangeDeclared;
    private bool _disposed;

    public RabbitMQPublisher(
        IRabbitMQConnection connection,
        IMessageSerializer serializer,
        PublisherOptions options,
        string publisherName,
        ILogger logger)
    {
        _connection = connection;
        _serializer = serializer;
        _options = options;
        _publisherName = publisherName;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        var result = await PublishWithConfirmAsync(message, cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Failed to publish message: {result.ErrorMessage}",
                result.Exception);
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(TMessage message, PublishOptions options, CancellationToken cancellationToken = default)
    {
        var result = await PublishWithConfirmAsync(message, options, cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Failed to publish message: {result.ErrorMessage}",
                result.Exception);
        }
    }

    /// <inheritdoc />
    public Task<PublishResult> PublishWithConfirmAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        return PublishWithConfirmAsync(message, PublishOptions.Default, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PublishResult> PublishWithConfirmAsync(
        TMessage message,
        PublishOptions options,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RabbitMQPublisher<TMessage>));

        var stopwatch = Stopwatch.StartNew();
        Activity? activity = null;

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await EnsureChannelAsync(cancellationToken);
            await EnsureExchangeAsync(channel, cancellationToken);

            var messageId = Guid.NewGuid().ToString();
            var body = _serializer.Serialize(message);
            var routingKey = options.RoutingKeyOverride ?? _options.RoutingKey;

            activity = RabbitXActivitySource.StartPublishActivity(
                _options.Exchange, routingKey, _publisherName, messageId, body.Length);

            var properties = CreateBasicProperties(channel, messageId, options);

            await channel.BasicPublishAsync(
                exchange: _options.Exchange,
                routingKey: routingKey,
                mandatory: _options.Mandatory,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            RabbitXMeter.MessagesPublished.Add(1,
                new KeyValuePair<string, object?>("publisher", _publisherName),
                new KeyValuePair<string, object?>("exchange", _options.Exchange));
            RabbitXMeter.PublishDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("publisher", _publisherName),
                new KeyValuePair<string, object?>("exchange", _options.Exchange));
            RabbitXMeter.PublishMessageSize.Record(body.Length,
                new KeyValuePair<string, object?>("publisher", _publisherName),
                new KeyValuePair<string, object?>("exchange", _options.Exchange));

            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogDebug(
                "Message published. Publisher: {Publisher}, Exchange: {Exchange}, RoutingKey: {RoutingKey}, MessageId: {MessageId}",
                _publisherName,
                _options.Exchange,
                routingKey,
                messageId);

            return PublishResult.Confirmed(messageId, options.CorrelationId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            RabbitXActivitySource.RecordException(activity, ex);
            RabbitXMeter.PublishErrors.Add(1,
                new KeyValuePair<string, object?>("publisher", _publisherName),
                new KeyValuePair<string, object?>("exchange", _options.Exchange));

            _logger.LogError(
                ex,
                "Failed to publish message. Publisher: {Publisher}, Exchange: {Exchange}",
                _publisherName,
                _options.Exchange);

            // Reset channel on error
            await ResetChannelAsync();

            return PublishResult.FromException(ex);
        }
        finally
        {
            activity?.Dispose();
            _channelLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublishResult>> PublishBatchWithConfirmAsync(
        IEnumerable<TMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PublishResult>();
        var messageList = messages.ToList();

        foreach (var message in messageList)
        {
            var result = await PublishWithConfirmAsync(message, cancellationToken);
            results.Add(result);

            // Stop on first failure if configured
            if (!result.Success)
            {
                _logger.LogWarning(
                    "Batch publish stopped after failure. Published: {Published}/{Total}",
                    results.Count,
                    messageList.Count);
                break;
            }
        }

        return results;
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        _channel = await _connection.CreateChannelAsync(
            enablePublisherConfirms: true,
            cancellationToken);

        _exchangeDeclared = false;

        _logger.LogDebug(
            "Channel created for publisher {Publisher}",
            _publisherName);

        return _channel;
    }

    private async Task EnsureExchangeAsync(IChannel channel, CancellationToken cancellationToken)
    {
        if (_exchangeDeclared)
            return;

        await channel.ExchangeDeclareAsync(
            exchange: _options.Exchange,
            type: _options.ExchangeType,
            durable: _options.Durable,
            autoDelete: _options.AutoDelete,
            arguments: _options.Arguments,
            cancellationToken: cancellationToken);

        _exchangeDeclared = true;

        _logger.LogDebug(
            "Exchange declared: {Exchange} (Type: {Type}, Durable: {Durable})",
            _options.Exchange,
            _options.ExchangeType,
            _options.Durable);
    }

    private BasicProperties CreateBasicProperties(
        IChannel channel,
        string messageId,
        PublishOptions options)
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-publisher-name"] = _publisherName,
            ["x-message-type"] = typeof(TMessage).FullName,
            ["x-published-at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Merge custom headers
        if (options.Headers != null)
        {
            foreach (var (key, value) in options.Headers)
            {
                headers[key] = value;
            }
        }

        // Inject trace context for distributed tracing propagation
        MessageHeadersPropagator.Inject(Activity.Current, headers);

        var properties = new BasicProperties
        {
            MessageId = messageId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = _serializer.ContentType,
            ContentEncoding = "utf-8",
            Persistent = _options.Persistent,
            Type = options.MessageType ?? typeof(TMessage).Name,
            Headers = headers
        };

        if (!string.IsNullOrEmpty(options.CorrelationId))
        {
            properties.CorrelationId = options.CorrelationId;
        }

        if (options.Expiration.HasValue)
        {
            properties.Expiration = options.Expiration.Value.TotalMilliseconds.ToString("0");
        }

        if (options.Priority.HasValue)
        {
            properties.Priority = options.Priority.Value;
        }

        if (!string.IsNullOrEmpty(options.AppId))
        {
            properties.AppId = options.AppId;
        }

        return properties;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is not null)
        {
            try
            {
                await _channel.CloseAsync();
                await _channel.DisposeAsync();
            }
            catch
            {
                // Ignore errors during cleanup
            }

            _channel = null;
            _exchangeDeclared = false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _channelLock.WaitAsync();
        try
        {
            await ResetChannelAsync();
        }
        finally
        {
            _channelLock.Release();
            _channelLock.Dispose();
        }
    }
}
