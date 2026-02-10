using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.Diagnostics;
using RabbitX.Interfaces;
using RabbitX.Models;
using RabbitX.Resilience;

namespace RabbitX.Consumers;

/// <summary>
/// RabbitMQ consumer with retry support, dead letter handling, and Polly integration.
/// </summary>
/// <typeparam name="TMessage">The type of message to consume.</typeparam>
internal sealed class RabbitMQConsumer<TMessage> : IMessageConsumer<TMessage>
    where TMessage : class
{
    private readonly IRabbitMQConnection _connection;
    private readonly IMessageSerializer _serializer;
    private readonly IMessageHandler<TMessage> _handler;
    private readonly IRetryPolicyProvider _retryPolicyProvider;
    private readonly ConsumerOptions _options;
    private readonly ILogger _logger;

    private IChannel? _channel;
    private string? _consumerTag;
    private bool _disposed;

    public RabbitMQConsumer(
        IRabbitMQConnection connection,
        IMessageSerializer serializer,
        IMessageHandler<TMessage> handler,
        IRetryPolicyProvider retryPolicyProvider,
        ConsumerOptions options,
        string consumerName,
        ILogger logger)
    {
        _connection = connection;
        _serializer = serializer;
        _handler = handler;
        _retryPolicyProvider = retryPolicyProvider;
        _options = options;
        ConsumerName = consumerName;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ConsumerName { get; }

    /// <inheritdoc />
    public bool IsRunning => _channel?.IsOpen == true && !string.IsNullOrEmpty(_consumerTag);

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RabbitMQConsumer<TMessage>));

        if (IsRunning)
        {
            _logger.LogWarning("Consumer {Consumer} is already running", ConsumerName);
            return;
        }

        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Configure QoS
        await _channel.BasicQosAsync(
            prefetchSize: _options.Qos.PrefetchSize,
            prefetchCount: _options.Qos.PrefetchCount,
            global: _options.Qos.Global,
            cancellationToken: cancellationToken);

        // Setup Dead Letter Exchange if configured
        if (_options.DeadLetter?.IsValid == true)
        {
            await SetupDeadLetterAsync(_channel, cancellationToken);
        }

        // Declare exchange
        if (!string.IsNullOrWhiteSpace(_options.Exchange))
        {
            await _channel.ExchangeDeclareAsync(
                exchange: _options.Exchange,
                type: _options.ExchangeType,
                durable: _options.Durable,
                autoDelete: _options.AutoDelete,
                cancellationToken: cancellationToken);
        }

        // Declare queue with arguments
        var queueArgs = BuildQueueArguments();

        await _channel.QueueDeclareAsync(
            queue: _options.Queue,
            durable: _options.Durable,
            exclusive: _options.Exclusive,
            autoDelete: _options.AutoDelete,
            arguments: queueArgs,
            cancellationToken: cancellationToken);

        // Bind queue to exchange
        if (!string.IsNullOrWhiteSpace(_options.Exchange))
        {
            await _channel.QueueBindAsync(
                queue: _options.Queue,
                exchange: _options.Exchange,
                routingKey: _options.RoutingKey,
                cancellationToken: cancellationToken);
        }

        // Create and start consumer
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        _consumerTag = await _channel.BasicConsumeAsync(
            queue: _options.Queue,
            autoAck: false, // Always manual ack for reliability
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Consumer {Consumer} started on queue {Queue}. PrefetchCount: {Prefetch}, MaxRetries: {MaxRetries}",
            ConsumerName,
            _options.Queue,
            _options.Qos.PrefetchCount,
            _options.Retry.MaxRetries);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            return;

        if (_channel is not null && !string.IsNullOrEmpty(_consumerTag))
        {
            try
            {
                await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
                _logger.LogInformation("Consumer {Consumer} stopped", ConsumerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error canceling consumer {Consumer}", ConsumerName);
            }
        }

        _consumerTag = null;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var messageId = args.BasicProperties.MessageId ?? Guid.NewGuid().ToString();
        var retryCount = GetRetryCount(args.BasicProperties);
        var stopwatch = Stopwatch.StartNew();

        // Extract parent trace context from message headers
        var parentContext = MessageHeadersPropagator.Extract(args.BasicProperties.Headers);
        using var activity = RabbitXActivitySource.StartConsumeActivity(
            _options.Queue, ConsumerName, messageId, parentContext);

        if (retryCount > 0)
            activity?.SetTag(RabbitXTelemetryConstants.RabbitXRetryCountKey, retryCount);

        RabbitXMeter.MessagesConsumed.Add(1,
            new KeyValuePair<string, object?>("consumer", ConsumerName),
            new KeyValuePair<string, object?>("queue", _options.Queue));

        _logger.LogDebug(
            "Message received. Consumer: {Consumer}, MessageId: {MessageId}, RetryCount: {RetryCount}",
            ConsumerName,
            messageId,
            retryCount);

        try
        {
            // Deserialize message
            var message = _serializer.Deserialize<TMessage>(args.Body.ToArray());

            // Build context
            var context = new MessageContext<TMessage>
            {
                Message = message,
                MessageId = messageId,
                CorrelationId = args.BasicProperties.CorrelationId,
                Timestamp = args.BasicProperties.Timestamp.UnixTime > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(args.BasicProperties.Timestamp.UnixTime)
                    : DateTimeOffset.UtcNow,
                RetryCount = retryCount,
                Exchange = args.Exchange,
                RoutingKey = args.RoutingKey,
                Queue = _options.Queue,
                ConsumerTag = args.ConsumerTag,
                Headers = args.BasicProperties.Headers?
                    .ToDictionary(h => h.Key, h => h.Value ?? new object())
                    ?? new Dictionary<string, object>(),
                DeliveryTag = args.DeliveryTag,
                Redelivered = args.Redelivered
            };

            // Process message with handler
            var result = await _handler.HandleAsync(context, CancellationToken.None);

            stopwatch.Stop();

            activity?.SetTag(RabbitXTelemetryConstants.RabbitXConsumeResultKey, result.ToString().ToLowerInvariant());
            activity?.SetStatus(ActivityStatusCode.Ok);

            RabbitXMeter.ConsumeResults.Add(1,
                new KeyValuePair<string, object?>("consumer", ConsumerName),
                new KeyValuePair<string, object?>("queue", _options.Queue),
                new KeyValuePair<string, object?>("result", result.ToString().ToLowerInvariant()));
            RabbitXMeter.ConsumeDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("consumer", ConsumerName),
                new KeyValuePair<string, object?>("queue", _options.Queue));

            // Handle result
            await HandleConsumeResultAsync(args, result, retryCount, messageId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            RabbitXActivitySource.RecordException(activity, ex);
            RabbitXMeter.ConsumeErrors.Add(1,
                new KeyValuePair<string, object?>("consumer", ConsumerName),
                new KeyValuePair<string, object?>("queue", _options.Queue));
            RabbitXMeter.ConsumeDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("consumer", ConsumerName),
                new KeyValuePair<string, object?>("queue", _options.Queue));

            _logger.LogError(
                ex,
                "Unhandled exception processing message {MessageId} in consumer {Consumer}",
                messageId,
                ConsumerName);

            // Treat unhandled exceptions as retry
            await HandleConsumeResultAsync(args, ConsumeResult.Retry, retryCount, messageId, ex);
        }
    }

    private async Task HandleConsumeResultAsync(
        BasicDeliverEventArgs args,
        ConsumeResult result,
        int retryCount,
        string messageId,
        Exception? exception = null)
    {
        switch (result)
        {
            case ConsumeResult.Ack:
                await _channel!.BasicAckAsync(args.DeliveryTag, false);
                _logger.LogDebug("Message {MessageId} acknowledged", messageId);
                break;

            case ConsumeResult.Retry:
                await HandleRetryAsync(args, retryCount, messageId, exception);
                break;

            case ConsumeResult.Reject:
                await HandleRejectAsync(args, messageId);
                break;

            case ConsumeResult.Defer:
                // Requeue for another consumer
                await _channel!.BasicNackAsync(args.DeliveryTag, false, true);
                _logger.LogInformation("Message {MessageId} deferred (requeued)", messageId);
                break;
        }
    }

    private async Task HandleRetryAsync(
        BasicDeliverEventArgs args,
        int currentRetryCount,
        string messageId,
        Exception? exception)
    {
        var retryOptions = _options.Retry;

        if (currentRetryCount >= retryOptions.MaxRetries)
        {
            RabbitXMeter.RetryExhausted.Add(1,
                new KeyValuePair<string, object?>("consumer", ConsumerName),
                new KeyValuePair<string, object?>("queue", _options.Queue));

            _logger.LogWarning(
                "Message {MessageId} exhausted all retries ({MaxRetries}). Action: {Action}",
                messageId,
                retryOptions.MaxRetries,
                retryOptions.OnRetryExhausted);

            await HandleRetryExhaustedAsync(args, messageId);
            return;
        }

        RabbitXMeter.RetryAttempts.Add(1,
            new KeyValuePair<string, object?>("consumer", ConsumerName),
            new KeyValuePair<string, object?>("queue", _options.Queue));

        // Calculate delay for this retry
        var delay = _retryPolicyProvider.GetDelayForRetry(currentRetryCount + 1, retryOptions);

        _logger.LogInformation(
            "Message {MessageId} scheduled for retry {Attempt}/{MaxRetries} after {Delay}s",
            messageId,
            currentRetryCount + 1,
            retryOptions.MaxRetries,
            delay.TotalSeconds);

        // Wait for delay
        await Task.Delay(delay);

        // Republish with incremented retry count
        await RepublishWithRetryAsync(args, currentRetryCount + 1, exception);

        // Acknowledge original message
        await _channel!.BasicAckAsync(args.DeliveryTag, false);
    }

    private async Task HandleRetryExhaustedAsync(BasicDeliverEventArgs args, string messageId)
    {
        switch (_options.Retry.OnRetryExhausted)
        {
            case RetryExhaustedAction.SendToDeadLetter:
                // NACK without requeue sends to DLX (if configured)
                await _channel!.BasicNackAsync(args.DeliveryTag, false, false);
                _logger.LogWarning("Message {MessageId} sent to dead letter queue", messageId);
                break;

            case RetryExhaustedAction.Requeue:
                await _channel!.BasicNackAsync(args.DeliveryTag, false, true);
                _logger.LogWarning("Message {MessageId} requeued after retry exhaustion", messageId);
                break;

            case RetryExhaustedAction.Discard:
                await _channel!.BasicAckAsync(args.DeliveryTag, false);
                _logger.LogWarning("Message {MessageId} discarded after retry exhaustion", messageId);
                break;
        }
    }

    private async Task HandleRejectAsync(BasicDeliverEventArgs args, string messageId)
    {
        // Immediate rejection - send to DLX
        await _channel!.BasicNackAsync(args.DeliveryTag, false, false);
        _logger.LogWarning("Message {MessageId} rejected and sent to dead letter queue", messageId);
    }

    private async Task RepublishWithRetryAsync(
        BasicDeliverEventArgs args,
        int newRetryCount,
        Exception? lastException)
    {
        var headers = args.BasicProperties.Headers != null
            ? new Dictionary<string, object?>(args.BasicProperties.Headers)
            : new Dictionary<string, object?>();

        headers["x-retry-count"] = newRetryCount;
        headers["x-original-message-id"] = args.BasicProperties.MessageId;
        headers["x-retry-timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        headers["x-last-error"] = lastException?.Message ?? "Unknown error";
        headers["x-consumer-name"] = ConsumerName;

        // Re-inject current trace context so the retried message maintains the trace
        MessageHeadersPropagator.Inject(Activity.Current, headers);

        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = args.BasicProperties.CorrelationId,
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = args.BasicProperties.ContentType,
            ContentEncoding = args.BasicProperties.ContentEncoding,
            Type = args.BasicProperties.Type,
            Headers = headers
        };

        await _channel!.BasicPublishAsync(
            exchange: _options.Exchange,
            routingKey: _options.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: args.Body);
    }

    private async Task SetupDeadLetterAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var dlx = _options.DeadLetter!;

        // Declare DLX exchange
        await channel.ExchangeDeclareAsync(
            exchange: dlx.Exchange,
            type: dlx.ExchangeType,
            durable: dlx.Durable,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Declare DLQ
        await channel.QueueDeclareAsync(
            queue: dlx.Queue,
            durable: dlx.Durable,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Bind DLQ to DLX
        await channel.QueueBindAsync(
            queue: dlx.Queue,
            exchange: dlx.Exchange,
            routingKey: dlx.RoutingKey,
            cancellationToken: cancellationToken);

        _logger.LogDebug(
            "Dead letter configured for consumer {Consumer}. Exchange: {DlxExchange}, Queue: {DlqQueue}",
            ConsumerName,
            dlx.Exchange,
            dlx.Queue);
    }

    private Dictionary<string, object?> BuildQueueArguments()
    {
        var args = new Dictionary<string, object?>();

        // Dead letter exchange
        if (_options.DeadLetter?.IsValid == true)
        {
            args["x-dead-letter-exchange"] = _options.DeadLetter.Exchange;
            args["x-dead-letter-routing-key"] = _options.DeadLetter.RoutingKey;
        }

        // Message TTL
        if (_options.MessageTtlSeconds.HasValue)
        {
            args["x-message-ttl"] = _options.MessageTtlSeconds.Value * 1000; // Convert to ms
        }

        // Custom arguments
        if (_options.Arguments != null)
        {
            foreach (var (key, value) in _options.Arguments)
            {
                args[key] = value;
            }
        }

        return args;
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers?.TryGetValue("x-retry-count", out var value) == true)
        {
            return Convert.ToInt32(value);
        }
        return 0;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await StopAsync(CancellationToken.None);

        if (_channel is not null)
        {
            try
            {
                await _channel.CloseAsync();
                await _channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing channel for consumer {Consumer}", ConsumerName);
            }
        }
    }
}
