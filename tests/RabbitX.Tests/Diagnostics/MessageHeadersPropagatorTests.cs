using System.Diagnostics;
using System.Text;
using FluentAssertions;
using RabbitX.Diagnostics;
using Xunit;

namespace RabbitX.Tests.Diagnostics;

public class MessageHeadersPropagatorTests : IDisposable
{
    private readonly ActivityListener _listener;

    public MessageHeadersPropagatorTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RabbitXTelemetryConstants.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    // ========================
    // Inject
    // ========================

    [Fact]
    public void Inject_WritesTraceparentHeader()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);
        activity.Should().NotBeNull();

        var headers = new Dictionary<string, object?>();

        // Act
        MessageHeadersPropagator.Inject(activity, headers);

        // Assert
        headers.Should().ContainKey("traceparent");
        var traceparent = headers["traceparent"] as string;
        traceparent.Should().NotBeNullOrEmpty();
        // W3C traceparent format: version-traceid-parentid-traceflags
        traceparent.Should().StartWith("00-");
        traceparent!.Split('-').Should().HaveCount(4);
    }

    [Fact]
    public void Inject_WithNullActivity_DoesNotModifyHeaders()
    {
        // Arrange
        var headers = new Dictionary<string, object?>
        {
            ["existing-header"] = "value"
        };

        // Act
        MessageHeadersPropagator.Inject(null, headers);

        // Assert
        headers.Should().HaveCount(1);
        headers.Should().ContainKey("existing-header");
        headers.Should().NotContainKey("traceparent");
    }

    [Fact]
    public void Inject_PreservesExistingHeaders()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);

        var headers = new Dictionary<string, object?>
        {
            ["x-custom-header"] = "custom-value",
            ["x-publisher-name"] = "TestPublisher",
            ["x-message-type"] = "OrderEvent"
        };

        // Act
        MessageHeadersPropagator.Inject(activity, headers);

        // Assert
        headers.Should().ContainKey("x-custom-header");
        headers["x-custom-header"].Should().Be("custom-value");
        headers.Should().ContainKey("x-publisher-name");
        headers["x-publisher-name"].Should().Be("TestPublisher");
        headers.Should().ContainKey("x-message-type");
        headers.Should().ContainKey("traceparent");
    }

    [Fact]
    public void Inject_TraceparentContainsActivityTraceIdAndSpanId()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);

        var headers = new Dictionary<string, object?>();

        // Act
        MessageHeadersPropagator.Inject(activity, headers);

        // Assert
        var traceparent = (string)headers["traceparent"]!;
        var parts = traceparent.Split('-');
        parts[1].Should().Be(activity!.TraceId.ToString());
        parts[2].Should().Be(activity.SpanId.ToString());
    }

    // ========================
    // Extract
    // ========================

    [Fact]
    public void Extract_WithNullHeaders_ReturnsDefaultContext()
    {
        // Act
        var context = MessageHeadersPropagator.Extract(null);

        // Assert
        context.Should().Be(default(ActivityContext));
    }

    [Fact]
    public void Extract_WithEmptyHeaders_ReturnsDefaultContext()
    {
        // Act
        var context = MessageHeadersPropagator.Extract(new Dictionary<string, object?>());

        // Assert
        context.Should().Be(default(ActivityContext));
    }

    [Fact]
    public void Extract_WithNoTraceHeaders_ReturnsDefaultTraceId()
    {
        // Arrange
        var headers = new Dictionary<string, object?>
        {
            ["x-custom-header"] = "value",
            ["x-retry-count"] = 3
        };

        // Act
        var context = MessageHeadersPropagator.Extract(headers);

        // Assert
        context.TraceId.Should().Be(default(ActivityTraceId));
    }

    [Fact]
    public void Extract_WithNullValueInHeaders_ReturnsDefaultContext()
    {
        // Arrange - simulate a header with null value
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = null
        };

        // Act
        var context = MessageHeadersPropagator.Extract(headers);

        // Assert
        context.TraceId.Should().Be(default(ActivityTraceId));
    }

    [Fact]
    public void Extract_WithIntegerValueForTraceparent_ReturnsDefaultContext()
    {
        // Arrange - wrong type for traceparent header
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = 12345
        };

        // Act
        var context = MessageHeadersPropagator.Extract(headers);

        // Assert
        context.TraceId.Should().Be(default(ActivityTraceId));
    }

    [Fact]
    public void Extract_WithStringTraceparent_ExtractsContext()
    {
        // Arrange - inject first to get a valid traceparent
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);
        var headers = new Dictionary<string, object?>();
        MessageHeadersPropagator.Inject(activity, headers);

        // Act
        var extracted = MessageHeadersPropagator.Extract(headers);

        // Assert
        extracted.TraceId.Should().Be(activity!.TraceId);
        extracted.SpanId.Should().Be(activity.SpanId);
    }

    [Fact]
    public void Extract_WithByteArrayTraceparent_DecodesAndExtractsContext()
    {
        // Arrange - inject then convert to byte[] (simulating RabbitMQ received headers)
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);

        var headers = new Dictionary<string, object?>();
        MessageHeadersPropagator.Inject(activity, headers);

        // Convert string values to byte[] as RabbitMQ does on the receiving side
        var receivedHeaders = headers.ToDictionary(
            h => h.Key,
            h => h.Value is string str ? (object?)Encoding.UTF8.GetBytes(str) : h.Value);

        // Act
        var extracted = MessageHeadersPropagator.Extract(receivedHeaders);

        // Assert
        extracted.TraceId.Should().Be(activity!.TraceId);
        extracted.SpanId.Should().Be(activity.SpanId);
    }

    // ========================
    // Round-Trip
    // ========================

    [Fact]
    public void RoundTrip_InjectAndExtract_PreservesTraceContext()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);
        activity.Should().NotBeNull();

        var headers = new Dictionary<string, object?>();

        // Act - inject
        MessageHeadersPropagator.Inject(activity, headers);

        // Act - extract
        var extractedContext = MessageHeadersPropagator.Extract(headers);

        // Assert
        extractedContext.TraceId.Should().Be(activity!.TraceId);
        extractedContext.SpanId.Should().Be(activity.SpanId);
    }

    [Fact]
    public void RoundTrip_WithByteArrayConversion_PreservesTraceContext()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);
        activity.Should().NotBeNull();

        var publishHeaders = new Dictionary<string, object?>();
        MessageHeadersPropagator.Inject(activity, publishHeaders);

        // Simulate network: string → byte[] as RabbitMQ broker does
        var consumeHeaders = publishHeaders.ToDictionary(
            h => h.Key,
            h => h.Value is string str ? (object?)Encoding.UTF8.GetBytes(str) : h.Value);

        // Act
        var extractedContext = MessageHeadersPropagator.Extract(consumeHeaders);

        // Assert
        extractedContext.TraceId.Should().Be(activity!.TraceId);
        extractedContext.SpanId.Should().Be(activity.SpanId);
    }

    // ========================
    // End-to-End: Publish → Consume Distributed Tracing
    // ========================

    [Fact]
    public void EndToEnd_PublishInject_ConsumeExtract_CreatesLinkedSpans()
    {
        // Arrange - Publisher side
        using var publishActivity = RabbitXActivitySource.StartPublishActivity(
            "orders.exchange", "order.created", "OrderPublisher", "msg-1", 256);
        publishActivity.Should().NotBeNull();

        var messageHeaders = new Dictionary<string, object?>
        {
            ["x-publisher-name"] = "OrderPublisher",
            ["x-message-type"] = "OrderEvent"
        };

        // Inject trace context into message headers
        MessageHeadersPropagator.Inject(publishActivity, messageHeaders);

        // Simulate RabbitMQ broker: convert string header values to byte[]
        var receivedHeaders = messageHeaders.ToDictionary(
            h => h.Key,
            h => h.Value is string str ? (object?)Encoding.UTF8.GetBytes(str) : h.Value);

        // Act - Consumer side
        var parentContext = MessageHeadersPropagator.Extract(receivedHeaders);
        using var consumeActivity = RabbitXActivitySource.StartConsumeActivity(
            "orders.queue", "OrderConsumer", "msg-1", parentContext);

        // Assert - Consumer activity is linked to Publisher via TraceId
        consumeActivity.Should().NotBeNull();
        consumeActivity!.TraceId.Should().Be(publishActivity!.TraceId,
            "consumer should be in the same distributed trace as the publisher");
        consumeActivity.ParentSpanId.Should().Be(publishActivity.SpanId,
            "consumer's parent should be the publisher span");
        consumeActivity.SpanId.Should().NotBe(publishActivity.SpanId,
            "consumer should have its own unique span");
    }

    [Fact]
    public void EndToEnd_RpcClient_InjectExtract_RpcServer_CreatesLinkedSpans()
    {
        // Arrange - RPC Client side
        using var clientActivity = RabbitXActivitySource.StartRpcClientActivity(
            "rpc.exchange", "rpc.orders", "OrderRpcClient", "corr-123");

        var requestHeaders = new Dictionary<string, object?>
        {
            ["x-rpc-client"] = "OrderRpcClient"
        };
        MessageHeadersPropagator.Inject(clientActivity, requestHeaders);

        // Simulate broker
        var receivedHeaders = requestHeaders.ToDictionary(
            h => h.Key,
            h => h.Value is string str ? (object?)Encoding.UTF8.GetBytes(str) : h.Value);

        // Act - RPC Server side
        var parentContext = MessageHeadersPropagator.Extract(receivedHeaders);
        using var serverActivity = RabbitXActivitySource.StartRpcServerActivity(
            "rpc.queue", "OrderRpcHandler", "corr-123", parentContext);

        // Assert
        serverActivity.Should().NotBeNull();
        serverActivity!.TraceId.Should().Be(clientActivity!.TraceId);
        serverActivity.ParentSpanId.Should().Be(clientActivity.SpanId);
    }

    [Fact]
    public void EndToEnd_RetryReInject_MaintainsTraceChain()
    {
        // Arrange - Original publish
        using var publishActivity = RabbitXActivitySource.StartPublishActivity(
            "orders.exchange", "order.created", "pub1", "msg-1", 100);

        var headers = new Dictionary<string, object?>();
        MessageHeadersPropagator.Inject(publishActivity, headers);

        // Simulate broker byte[] conversion
        var receivedHeaders = headers.ToDictionary(
            h => h.Key,
            h => h.Value is string str ? (object?)Encoding.UTF8.GetBytes(str) : h.Value);

        // Consumer extracts and starts activity
        var parentContext = MessageHeadersPropagator.Extract(receivedHeaders);
        using var consumeActivity = RabbitXActivitySource.StartConsumeActivity(
            "orders.queue", "consumer1", "msg-1", parentContext);

        // Simulate retry: re-inject current activity context into new headers
        var retryHeaders = new Dictionary<string, object?>
        {
            ["x-retry-count"] = 1
        };
        MessageHeadersPropagator.Inject(consumeActivity, retryHeaders);

        // Simulate broker again
        var retryReceivedHeaders = retryHeaders.ToDictionary(
            h => h.Key,
            h => h.Value is string str ? (object?)Encoding.UTF8.GetBytes(str) : h.Value);

        // Act - Second consumer picks up retried message
        var retryParentContext = MessageHeadersPropagator.Extract(retryReceivedHeaders);
        using var retryConsumeActivity = RabbitXActivitySource.StartConsumeActivity(
            "orders.queue", "consumer1", "msg-1-retry", retryParentContext);

        // Assert - All three spans share the same TraceId
        retryConsumeActivity.Should().NotBeNull();
        retryConsumeActivity!.TraceId.Should().Be(publishActivity!.TraceId,
            "retry consume should still be in the same distributed trace");
        retryConsumeActivity.ParentSpanId.Should().Be(consumeActivity!.SpanId,
            "retry consume parent should be the first consume span");
    }

    // ========================
    // Mixed Header Types
    // ========================

    [Fact]
    public void Extract_WithMixedHeaderTypes_HandlesGracefully()
    {
        // Arrange - inject first to get valid traceparent
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);

        var headers = new Dictionary<string, object?>();
        MessageHeadersPropagator.Inject(activity, headers);

        // Add mixed-type headers alongside traceparent
        headers["x-retry-count"] = 3;           // int
        headers["x-published-at"] = 1700000000L; // long
        headers["x-nullable"] = null;            // null

        // Act
        var extracted = MessageHeadersPropagator.Extract(headers);

        // Assert - trace context still extracted correctly despite mixed types
        extracted.TraceId.Should().Be(activity!.TraceId);
        extracted.SpanId.Should().Be(activity.SpanId);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}
