namespace RabbitX.Diagnostics;

/// <summary>
/// Semantic convention constants for RabbitX OpenTelemetry instrumentation.
/// Follows OpenTelemetry Messaging Semantic Conventions.
/// </summary>
internal static class RabbitXTelemetryConstants
{
    /// <summary>
    /// The name of the ActivitySource used by RabbitX.
    /// </summary>
    public const string ActivitySourceName = "RabbitX";

    /// <summary>
    /// The name of the Meter used by RabbitX.
    /// </summary>
    public const string MeterName = "RabbitX";

    // --- OTel Messaging Semantic Conventions ---

    /// <summary>messaging.system attribute value.</summary>
    public const string MessagingSystem = "rabbitmq";

    // Attribute keys
    public const string MessagingSystemKey = "messaging.system";
    public const string MessagingDestinationNameKey = "messaging.destination.name";
    public const string MessagingOperationTypeKey = "messaging.operation.type";
    public const string MessagingMessageIdKey = "messaging.message.id";
    public const string MessagingMessageBodySizeKey = "messaging.message.body.size";

    // RabbitMQ-specific
    public const string MessagingRabbitMQRoutingKeyKey = "messaging.rabbitmq.destination.routing_key";

    // Operation type values
    public const string OperationPublish = "publish";
    public const string OperationProcess = "process";

    // Custom RabbitX attributes
    public const string RabbitXPublisherNameKey = "rabbitx.publisher.name";
    public const string RabbitXConsumerNameKey = "rabbitx.consumer.name";
    public const string RabbitXRpcClientNameKey = "rabbitx.rpc.client.name";
    public const string RabbitXRpcHandlerNameKey = "rabbitx.rpc.handler.name";
    public const string RabbitXRetryCountKey = "rabbitx.retry.count";
    public const string RabbitXConsumeResultKey = "rabbitx.consume.result";

    // W3C TraceContext header names
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";
}
