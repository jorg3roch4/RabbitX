using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.Interfaces;
using RabbitX.Models;

namespace RabbitX.Rpc;

/// <summary>
/// RabbitMQ implementation of RPC client using the Request-Reply pattern.
/// </summary>
/// <typeparam name="TRequest">The request message type.</typeparam>
/// <typeparam name="TResponse">The response message type.</typeparam>
internal sealed class RabbitMQRpcClient<TRequest, TResponse> : IRpcClient<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly RpcClientOptions _options;
    private readonly IRabbitMQConnection _connection;
    private readonly IRpcPendingRequests _pendingRequests;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger _logger;
    private readonly string _clientId;

    private IChannel? _channel;
    private string? _replyQueueName;
    private AsyncEventingBasicConsumer? _replyConsumer;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private bool _exchangeDeclared;
    private bool _replyConsumerStarted;
    private bool _disposed;

    /// <summary>
    /// Creates a new RPC client instance.
    /// </summary>
    public RabbitMQRpcClient(
        RpcClientOptions options,
        IRabbitMQConnection connection,
        IRpcPendingRequests pendingRequests,
        IMessageSerializer serializer,
        ILogger logger)
    {
        _options = options;
        _connection = connection;
        _pendingRequests = pendingRequests;
        _serializer = serializer;
        _logger = logger;
        _clientId = Guid.NewGuid().ToString("N")[..8];
    }

    /// <inheritdoc />
    public async Task<TResponse> CallAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var result = await TryCallAsync(request, cancellationToken);

        if (!result.Success)
        {
            if (result.IsTimeout)
            {
                throw new TimeoutException(result.ErrorMessage);
            }

            throw new InvalidOperationException(
                result.ErrorMessage ?? "RPC call failed",
                result.Exception);
        }

        return result.Response!;
    }

    /// <inheritdoc />
    public async Task<TResponse> CallAsync(TRequest request, RpcCallOptions options, CancellationToken cancellationToken = default)
    {
        var result = await TryCallInternalAsync(request, options, cancellationToken);

        if (!result.Success)
        {
            if (result.IsTimeout)
            {
                throw new TimeoutException(result.ErrorMessage);
            }

            throw new InvalidOperationException(
                result.ErrorMessage ?? "RPC call failed",
                result.Exception);
        }

        return result.Response!;
    }

    /// <inheritdoc />
    public Task<RpcResult<TResponse>> TryCallAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        return TryCallInternalAsync(request, RpcCallOptions.Default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RpcResult<TResponse>> TryCallAsync(TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return TryCallInternalAsync(request, RpcCallOptions.WithTimeout(timeout), cancellationToken);
    }

    private async Task<RpcResult<TResponse>> TryCallInternalAsync(
        TRequest request,
        RpcCallOptions options,
        CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RabbitMQRpcClient<TRequest, TResponse>));

        var correlationId = options.CorrelationId ?? Guid.NewGuid().ToString();
        var timeout = options.Timeout ?? _options.Timeout;
        var stopwatch = Stopwatch.StartNew();

        // Create TaskCompletionSource to wait for response
        var tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Register in pending requests
        _pendingRequests.Register(correlationId, tcs, timeout);

        try
        {
            await _channelLock.WaitAsync(cancellationToken);
            try
            {
                // Ensure channel and reply consumer are set up
                var channel = await EnsureChannelAsync(cancellationToken);
                await EnsureExchangeAsync(channel, cancellationToken);
                await EnsureReplyConsumerAsync(channel, cancellationToken);

                // Create message properties
                var properties = CreateBasicProperties(correlationId, options);

                // Serialize and publish request
                var body = _serializer.Serialize(request);

                await channel.BasicPublishAsync(
                    exchange: _options.Exchange,
                    routingKey: _options.RoutingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                _logger.LogDebug(
                    "RPC request published. Client: {ClientName}, CorrelationId: {CorrelationId}, ReplyTo: {ReplyTo}",
                    _options.Name,
                    correlationId,
                    _replyQueueName);
            }
            finally
            {
                _channelLock.Release();
            }

            // Wait for response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var response = await tcs.Task.WaitAsync(cts.Token);
            stopwatch.Stop();

            _logger.LogDebug(
                "RPC response received. Client: {ClientName}, CorrelationId: {CorrelationId}, Duration: {Duration}ms",
                _options.Name,
                correlationId,
                stopwatch.ElapsedMilliseconds);

            return RpcResult<TResponse>.Ok(response, correlationId, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                "RPC timeout. Client: {ClientName}, CorrelationId: {CorrelationId}, Timeout: {Timeout}s",
                _options.Name,
                correlationId,
                timeout.TotalSeconds);

            return RpcResult<TResponse>.Timeout(correlationId, timeout);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "RPC call failed. Client: {ClientName}, CorrelationId: {CorrelationId}",
                _options.Name,
                correlationId);

            return RpcResult<TResponse>.FromException(correlationId, ex);
        }
        finally
        {
            // Ensure pending request is removed
            _pendingRequests.TryRemove(correlationId);
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        _channel = await _connection.CreateChannelAsync(
            enablePublisherConfirms: false,
            cancellationToken);

        _exchangeDeclared = false;
        _replyConsumerStarted = false;

        _logger.LogDebug(
            "Channel created for RPC client {ClientName}",
            _options.Name);

        return _channel;
    }

    private async Task EnsureExchangeAsync(IChannel channel, CancellationToken cancellationToken)
    {
        if (_exchangeDeclared || !_options.DeclareExchange)
            return;

        await channel.ExchangeDeclareAsync(
            exchange: _options.Exchange,
            type: _options.ExchangeType,
            durable: _options.Durable,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        _exchangeDeclared = true;

        _logger.LogDebug(
            "Exchange declared for RPC client: {Exchange} (Type: {Type})",
            _options.Exchange,
            _options.ExchangeType);
    }

    private async Task EnsureReplyConsumerAsync(IChannel channel, CancellationToken cancellationToken)
    {
        if (_replyConsumerStarted)
            return;

        if (_options.UseDirectReplyTo)
        {
            // Use RabbitMQ's Direct Reply-to feature
            _replyQueueName = "amq.rabbitmq.reply-to";
        }
        else
        {
            // Declare a custom reply queue
            var prefix = _options.ReplyQueuePrefix ?? "rpc.reply";
            var queueName = $"{prefix}.{_options.Name}.{_clientId}";

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: false,
                exclusive: true,
                autoDelete: true,
                arguments: null,
                cancellationToken: cancellationToken);

            _replyQueueName = queueName;

            _logger.LogDebug(
                "Reply queue declared: {QueueName}",
                _replyQueueName);
        }

        // Create and start consumer
        _replyConsumer = new AsyncEventingBasicConsumer(channel);
        _replyConsumer.ReceivedAsync += HandleReplyAsync;

        await channel.BasicConsumeAsync(
            queue: _replyQueueName,
            autoAck: true,
            consumer: _replyConsumer,
            cancellationToken: cancellationToken);

        _replyConsumerStarted = true;

        _logger.LogDebug(
            "Reply consumer started for RPC client {ClientName} on queue {Queue}",
            _options.Name,
            _replyQueueName);
    }

    private Task HandleReplyAsync(object sender, BasicDeliverEventArgs args)
    {
        var correlationId = args.BasicProperties.CorrelationId;

        if (string.IsNullOrEmpty(correlationId))
        {
            _logger.LogWarning(
                "Received RPC response without correlation ID");
            return Task.CompletedTask;
        }

        // Complete the pending request
        if (_pendingRequests.TryComplete(correlationId, args.Body))
        {
            _logger.LogDebug(
                "RPC response processed. CorrelationId: {CorrelationId}",
                correlationId);
        }

        return Task.CompletedTask;
    }

    private BasicProperties CreateBasicProperties(string correlationId, RpcCallOptions options)
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-rpc-client"] = _options.Name,
            ["x-request-type"] = typeof(TRequest).FullName,
            ["x-response-type"] = typeof(TResponse).FullName
        };

        // Merge custom headers
        if (options.Headers != null)
        {
            foreach (var (key, value) in options.Headers)
            {
                headers[key] = value;
            }
        }

        return new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = _replyQueueName,
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = _serializer.ContentType,
            ContentEncoding = "utf-8",
            Persistent = false, // RPC messages are typically transient
            Type = typeof(TRequest).Name,
            Headers = headers
        };
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
            }
        }
        finally
        {
            _channelLock.Release();
            _channelLock.Dispose();
        }

        _logger.LogDebug(
            "RPC client disposed: {ClientName}",
            _options.Name);
    }
}
