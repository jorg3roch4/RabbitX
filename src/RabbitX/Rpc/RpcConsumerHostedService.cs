using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.Diagnostics;
using RabbitX.Interfaces;

namespace RabbitX.Rpc;

/// <summary>
/// Hosted service that processes RPC requests and sends responses.
/// </summary>
public sealed class RpcConsumerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitXOptions _options;
    private readonly IRabbitMQConnection _connection;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<RpcConsumerHostedService> _logger;
    private readonly List<RpcHandlerContext> _handlers = new();

    /// <summary>
    /// Creates a new RPC consumer hosted service.
    /// </summary>
    public RpcConsumerHostedService(
        IServiceProvider serviceProvider,
        RabbitXOptions options,
        IRabbitMQConnection connection,
        IMessageSerializer serializer,
        ILogger<RpcConsumerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _connection = connection;
        _serializer = serializer;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.RpcHandlers.Any())
        {
            _logger.LogDebug("No RPC handlers configured, service will not start consumers");
            return;
        }

        _logger.LogInformation(
            "Starting RPC consumer service with {Count} handler(s)",
            _options.RpcHandlers.Count);

        foreach (var (name, handlerOptions) in _options.RpcHandlers)
        {
            try
            {
                await StartHandlerAsync(name, handlerOptions, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start RPC handler {Name}", name);
                throw;
            }
        }

        _logger.LogInformation("All RPC handlers started successfully");

        // Keep running until cancelled
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RPC consumer service is stopping");
        }
    }

    private async Task StartHandlerAsync(string name, RpcHandlerOptions options, CancellationToken cancellationToken)
    {
        var channel = await _connection.CreateChannelAsync(
            enablePublisherConfirms: false,
            cancellationToken);

        // Set QoS
        await channel.BasicQosAsync(0, options.PrefetchCount, false, cancellationToken);

        // Declare queue if configured
        if (options.DeclareQueue)
        {
            await channel.QueueDeclareAsync(
                queue: options.Queue,
                durable: options.Durable,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "RPC queue declared: {Queue} (Durable: {Durable})",
                options.Queue,
                options.Durable);

            // Bind to exchange if specified
            if (!string.IsNullOrEmpty(options.Exchange) && !string.IsNullOrEmpty(options.RoutingKey))
            {
                // Declare exchange if it doesn't exist (passive declare will fail, so we declare it)
                await channel.ExchangeDeclareAsync(
                    exchange: options.Exchange,
                    type: options.ExchangeType,
                    durable: options.Durable,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken);

                _logger.LogDebug(
                    "RPC exchange declared: {Exchange} (Type: {Type})",
                    options.Exchange,
                    options.ExchangeType);

                await channel.QueueBindAsync(
                    queue: options.Queue,
                    exchange: options.Exchange,
                    routingKey: options.RoutingKey,
                    arguments: null,
                    cancellationToken: cancellationToken);

                _logger.LogDebug(
                    "RPC queue bound: {Queue} -> {Exchange}/{RoutingKey}",
                    options.Queue,
                    options.Exchange,
                    options.RoutingKey);
            }
        }

        // Create consumer
        var consumer = new AsyncEventingBasicConsumer(channel);

        var context = new RpcHandlerContext
        {
            Name = name,
            Options = options,
            Channel = channel,
            Consumer = consumer
        };

        consumer.ReceivedAsync += async (sender, args) =>
        {
            await HandleRpcRequestAsync(context, args, cancellationToken);
        };

        // Start consuming
        await channel.BasicConsumeAsync(
            queue: options.Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _handlers.Add(context);

        _logger.LogInformation(
            "RPC handler started: {Name} on queue {Queue}",
            name,
            options.Queue);
    }

    private async Task HandleRpcRequestAsync(
        RpcHandlerContext context,
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken)
    {
        var correlationId = args.BasicProperties.CorrelationId;
        var replyTo = args.BasicProperties.ReplyTo;
        var messageId = args.BasicProperties.MessageId ?? Guid.NewGuid().ToString();

        // Extract parent trace context from request headers
        var parentContext = MessageHeadersPropagator.Extract(args.BasicProperties.Headers);
        using var activity = RabbitXActivitySource.StartRpcServerActivity(
            context.Options.Queue, context.Name, correlationId ?? messageId, parentContext);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrEmpty(replyTo))
            {
                _logger.LogWarning(
                    "RPC request missing ReplyTo. Handler: {Handler}, MessageId: {MessageId}",
                    context.Name,
                    messageId);

                await context.Channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
                return;
            }

            if (string.IsNullOrEmpty(correlationId))
            {
                _logger.LogWarning(
                    "RPC request missing CorrelationId. Handler: {Handler}, MessageId: {MessageId}",
                    context.Name,
                    messageId);

                await context.Channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
                return;
            }

            _logger.LogDebug(
                "Processing RPC request. Handler: {Handler}, CorrelationId: {CorrelationId}",
                context.Name,
                correlationId);

            // Deserialize request
            var requestType = context.Options.RequestType!;
            var request = _serializer.Deserialize(args.Body, requestType);

            // Create RPC context using reflection (to work with generic IRpcHandler)
            var rpcContextType = typeof(RpcContext<>).MakeGenericType(requestType);
            var rpcContext = Activator.CreateInstance(rpcContextType);

            // Set properties using reflection
            var messageProperty = rpcContextType.GetProperty("Message")!;
            var messageIdProperty = rpcContextType.GetProperty("MessageId")!;
            var correlationIdProperty = rpcContextType.GetProperty("CorrelationId")!;
            var replyToProperty = rpcContextType.GetProperty("ReplyTo")!;
            var timestampProperty = rpcContextType.GetProperty("Timestamp")!;

            messageProperty.SetValue(rpcContext, request);
            messageIdProperty.SetValue(rpcContext, messageId);
            correlationIdProperty.SetValue(rpcContext, correlationId);
            replyToProperty.SetValue(rpcContext, replyTo);
            timestampProperty.SetValue(rpcContext, DateTimeOffset.UtcNow);

            // Resolve handler from DI
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService(context.Options.HandlerType!);

            // Invoke HandleAsync using reflection
            var handleMethod = context.Options.HandlerType!.GetMethod("HandleAsync")!;
            var resultTask = (Task)handleMethod.Invoke(handler, [rpcContext, cancellationToken])!;
            await resultTask;

            // Get result from Task<TResponse>
            var resultProperty = resultTask.GetType().GetProperty("Result")!;
            var response = resultProperty.GetValue(resultTask)!;

            // Serialize response
            var responseBody = SerializeResponse(response, context.Options.ResponseType!);

            // Send response
            var responseProperties = new BasicProperties
            {
                CorrelationId = correlationId,
                ContentType = _serializer.ContentType,
                ContentEncoding = "utf-8",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await context.Channel.BasicPublishAsync(
                exchange: "",
                routingKey: replyTo,
                mandatory: false,
                basicProperties: responseProperties,
                body: responseBody,
                cancellationToken: cancellationToken);

            // Acknowledge request
            await context.Channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);

            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogDebug(
                "RPC response sent. Handler: {Handler}, CorrelationId: {CorrelationId}",
                context.Name,
                correlationId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RabbitXActivitySource.RecordException(activity, ex);
            RabbitXMeter.RpcErrors.Add(1,
                new KeyValuePair<string, object?>("handler", context.Name),
                new KeyValuePair<string, object?>("queue", context.Options.Queue));

            _logger.LogError(
                ex,
                "Error processing RPC request. Handler: {Handler}, CorrelationId: {CorrelationId}",
                context.Name,
                correlationId);

            // Nack without requeue - RPC failures should be reported back to caller
            try
            {
                await context.Channel.BasicNackAsync(args.DeliveryTag, false, false, cancellationToken);
            }
            catch
            {
                // Ignore nack errors
            }
        }
    }

    private byte[] SerializeResponse(object response, Type responseType)
    {
        // Use reflection to call generic Serialize method
        var method = typeof(IMessageSerializer)
            .GetMethod("Serialize")!
            .MakeGenericMethod(responseType);

        return (byte[])method.Invoke(_serializer, [response])!;
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping RPC consumer service");

        foreach (var handler in _handlers)
        {
            try
            {
                if (handler.Channel is { IsOpen: true })
                {
                    await handler.Channel.CloseAsync(cancellationToken);
                    await handler.Channel.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing RPC handler channel: {Name}", handler.Name);
            }
        }

        _handlers.Clear();

        await base.StopAsync(cancellationToken);
    }

    private sealed class RpcHandlerContext
    {
        public required string Name { get; init; }
        public required RpcHandlerOptions Options { get; init; }
        public required IChannel Channel { get; init; }
        public required AsyncEventingBasicConsumer Consumer { get; init; }
    }
}
