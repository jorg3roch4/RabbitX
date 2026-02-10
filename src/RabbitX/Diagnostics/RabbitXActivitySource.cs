namespace RabbitX.Diagnostics;

/// <summary>
/// Provides factory methods for creating OpenTelemetry Activities for RabbitX operations.
/// When no OTel listener is configured, StartActivity returns null with zero overhead.
/// </summary>
internal static class RabbitXActivitySource
{
    private static readonly ActivitySource Source = new(
        RabbitXTelemetryConstants.ActivitySourceName,
        typeof(RabbitXActivitySource).Assembly.GetName().Version?.ToString() ?? "1.0.0");

    /// <summary>
    /// Starts a new Activity for a publish operation.
    /// </summary>
    /// <param name="exchange">The target exchange name.</param>
    /// <param name="routingKey">The routing key.</param>
    /// <param name="publisherName">The publisher name.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="bodySize">The serialized message body size in bytes.</param>
    /// <returns>An Activity if a listener is registered; otherwise null.</returns>
    public static Activity? StartPublishActivity(
        string exchange,
        string routingKey,
        string publisherName,
        string messageId,
        int bodySize)
    {
        var activity = Source.StartActivity(
            $"{exchange} publish",
            ActivityKind.Producer);

        if (activity is null)
            return null;

        activity.SetTag(RabbitXTelemetryConstants.MessagingSystemKey, RabbitXTelemetryConstants.MessagingSystem);
        activity.SetTag(RabbitXTelemetryConstants.MessagingDestinationNameKey, exchange);
        activity.SetTag(RabbitXTelemetryConstants.MessagingOperationTypeKey, RabbitXTelemetryConstants.OperationPublish);
        activity.SetTag(RabbitXTelemetryConstants.MessagingMessageIdKey, messageId);
        activity.SetTag(RabbitXTelemetryConstants.MessagingMessageBodySizeKey, bodySize);
        activity.SetTag(RabbitXTelemetryConstants.MessagingRabbitMQRoutingKeyKey, routingKey);
        activity.SetTag(RabbitXTelemetryConstants.RabbitXPublisherNameKey, publisherName);

        return activity;
    }

    /// <summary>
    /// Starts a new Activity for a consume operation.
    /// </summary>
    /// <param name="queue">The source queue name.</param>
    /// <param name="consumerName">The consumer name.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="parentContext">Optional parent activity context extracted from message headers.</param>
    /// <returns>An Activity if a listener is registered; otherwise null.</returns>
    public static Activity? StartConsumeActivity(
        string queue,
        string consumerName,
        string messageId,
        ActivityContext parentContext = default)
    {
        var activity = parentContext != default
            ? Source.StartActivity(
                $"{queue} process",
                ActivityKind.Consumer,
                parentContext)
            : Source.StartActivity(
                $"{queue} process",
                ActivityKind.Consumer);

        if (activity is null)
            return null;

        activity.SetTag(RabbitXTelemetryConstants.MessagingSystemKey, RabbitXTelemetryConstants.MessagingSystem);
        activity.SetTag(RabbitXTelemetryConstants.MessagingDestinationNameKey, queue);
        activity.SetTag(RabbitXTelemetryConstants.MessagingOperationTypeKey, RabbitXTelemetryConstants.OperationProcess);
        activity.SetTag(RabbitXTelemetryConstants.MessagingMessageIdKey, messageId);
        activity.SetTag(RabbitXTelemetryConstants.RabbitXConsumerNameKey, consumerName);

        return activity;
    }

    /// <summary>
    /// Starts a new Activity for an RPC client call.
    /// </summary>
    /// <param name="exchange">The target exchange name.</param>
    /// <param name="routingKey">The routing key.</param>
    /// <param name="clientName">The RPC client name.</param>
    /// <param name="correlationId">The RPC correlation ID.</param>
    /// <returns>An Activity if a listener is registered; otherwise null.</returns>
    public static Activity? StartRpcClientActivity(
        string exchange,
        string routingKey,
        string clientName,
        string correlationId)
    {
        var activity = Source.StartActivity(
            $"{exchange} publish",
            ActivityKind.Client);

        if (activity is null)
            return null;

        activity.SetTag(RabbitXTelemetryConstants.MessagingSystemKey, RabbitXTelemetryConstants.MessagingSystem);
        activity.SetTag(RabbitXTelemetryConstants.MessagingDestinationNameKey, exchange);
        activity.SetTag(RabbitXTelemetryConstants.MessagingOperationTypeKey, RabbitXTelemetryConstants.OperationPublish);
        activity.SetTag(RabbitXTelemetryConstants.MessagingRabbitMQRoutingKeyKey, routingKey);
        activity.SetTag(RabbitXTelemetryConstants.RabbitXRpcClientNameKey, clientName);
        activity.SetTag(RabbitXTelemetryConstants.MessagingMessageIdKey, correlationId);

        return activity;
    }

    /// <summary>
    /// Starts a new Activity for an RPC server handler.
    /// </summary>
    /// <param name="queue">The source queue name.</param>
    /// <param name="handlerName">The RPC handler name.</param>
    /// <param name="correlationId">The RPC correlation ID.</param>
    /// <param name="parentContext">Optional parent activity context extracted from message headers.</param>
    /// <returns>An Activity if a listener is registered; otherwise null.</returns>
    public static Activity? StartRpcServerActivity(
        string queue,
        string handlerName,
        string correlationId,
        ActivityContext parentContext = default)
    {
        var activity = parentContext != default
            ? Source.StartActivity(
                $"{queue} process",
                ActivityKind.Server,
                parentContext)
            : Source.StartActivity(
                $"{queue} process",
                ActivityKind.Server);

        if (activity is null)
            return null;

        activity.SetTag(RabbitXTelemetryConstants.MessagingSystemKey, RabbitXTelemetryConstants.MessagingSystem);
        activity.SetTag(RabbitXTelemetryConstants.MessagingDestinationNameKey, queue);
        activity.SetTag(RabbitXTelemetryConstants.MessagingOperationTypeKey, RabbitXTelemetryConstants.OperationProcess);
        activity.SetTag(RabbitXTelemetryConstants.RabbitXRpcHandlerNameKey, handlerName);
        activity.SetTag(RabbitXTelemetryConstants.MessagingMessageIdKey, correlationId);

        return activity;
    }

    /// <summary>
    /// Records an exception on an Activity and sets status to Error.
    /// </summary>
    /// <param name="activity">The activity to record on (may be null).</param>
    /// <param name="exception">The exception to record.</param>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().FullName },
            { "exception.message", exception.Message },
            { "exception.stacktrace", exception.ToString() }
        }));
    }
}
