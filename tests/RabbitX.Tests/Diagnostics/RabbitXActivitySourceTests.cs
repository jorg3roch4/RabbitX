using System.Diagnostics;
using FluentAssertions;
using RabbitX.Diagnostics;
using Xunit;

namespace RabbitX.Tests.Diagnostics;

public class RabbitXActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _capturedActivities = new();

    public RabbitXActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RabbitXTelemetryConstants.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    // ========================
    // Publish Activity
    // ========================

    [Fact]
    public void StartPublishActivity_CreatesProducerActivity_WithAllSemanticTags()
    {
        // Act
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "orders.exchange", "order.created", "OrderPublisher", "msg-123", 256);

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Producer);
        activity.DisplayName.Should().Be("orders.exchange publish");

        // OTel messaging semantic conventions
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingSystemKey).Should().Be("rabbitmq");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingDestinationNameKey).Should().Be("orders.exchange");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingOperationTypeKey).Should().Be("publish");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingMessageIdKey).Should().Be("msg-123");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingMessageBodySizeKey).Should().Be(256);
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingRabbitMQRoutingKeyKey).Should().Be("order.created");

        // Custom RabbitX attributes
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXPublisherNameKey).Should().Be("OrderPublisher");
    }

    [Fact]
    public void StartPublishActivity_DoesNotIncludeConsumerSpecificTags()
    {
        // Act
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "pub1", "msg-1", 100);

        // Assert - consumer/rpc tags should not be present
        activity.Should().NotBeNull();
        activity!.GetTagItem(RabbitXTelemetryConstants.RabbitXConsumerNameKey).Should().BeNull();
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXRpcClientNameKey).Should().BeNull();
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXRpcHandlerNameKey).Should().BeNull();
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXRetryCountKey).Should().BeNull();
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXConsumeResultKey).Should().BeNull();
    }

    [Fact]
    public void StartPublishActivity_WithZeroBodySize_RecordsSizeTag()
    {
        // Act
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "pub1", "msg-1", 0);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem(RabbitXTelemetryConstants.MessagingMessageBodySizeKey).Should().Be(0);
    }

    [Fact]
    public void StartPublishActivity_HasValidTraceIdAndSpanId()
    {
        // Act
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "pub1", "msg-1", 100);

        // Assert
        activity.Should().NotBeNull();
        activity!.TraceId.Should().NotBe(default(ActivityTraceId));
        activity.SpanId.Should().NotBe(default(ActivitySpanId));
    }

    // ========================
    // Consume Activity
    // ========================

    [Fact]
    public void StartConsumeActivity_CreatesConsumerActivity_WithAllSemanticTags()
    {
        // Act
        using var activity = RabbitXActivitySource.StartConsumeActivity(
            "orders.queue", "OrderConsumer", "msg-456");

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Consumer);
        activity.DisplayName.Should().Be("orders.queue process");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingSystemKey).Should().Be("rabbitmq");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingDestinationNameKey).Should().Be("orders.queue");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingOperationTypeKey).Should().Be("process");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingMessageIdKey).Should().Be("msg-456");
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXConsumerNameKey).Should().Be("OrderConsumer");
    }

    [Fact]
    public void StartConsumeActivity_DoesNotIncludeBodySizeTag()
    {
        // Act
        using var activity = RabbitXActivitySource.StartConsumeActivity(
            "test.queue", "consumer1", "msg-1");

        // Assert - body size is publish-only
        activity.Should().NotBeNull();
        activity!.GetTagItem(RabbitXTelemetryConstants.MessagingMessageBodySizeKey).Should().BeNull();
    }

    [Fact]
    public void StartConsumeActivity_WithParentContext_InheritsTraceId()
    {
        // Arrange
        using var parentActivity = RabbitXActivitySource.StartPublishActivity(
            "orders.exchange", "order.created", "publisher", "msg-1", 100);
        var parentContext = parentActivity!.Context;

        // Act
        using var childActivity = RabbitXActivitySource.StartConsumeActivity(
            "orders.queue", "OrderConsumer", "msg-1", parentContext);

        // Assert
        childActivity.Should().NotBeNull();
        childActivity!.Kind.Should().Be(ActivityKind.Consumer);
        childActivity.ParentId.Should().NotBeNullOrEmpty();
        // Child inherits the TraceId from parent (same distributed trace)
        childActivity.TraceId.Should().Be(parentActivity.TraceId);
        // But has its own SpanId
        childActivity.SpanId.Should().NotBe(parentActivity.SpanId);
    }

    [Fact]
    public void StartConsumeActivity_WithDefaultContext_HasNoParent()
    {
        // Act
        using var activity = RabbitXActivitySource.StartConsumeActivity(
            "orders.queue", "OrderConsumer", "msg-1", default);

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Consumer);
        activity.TraceId.Should().NotBe(default(ActivityTraceId));
    }

    // ========================
    // RPC Client Activity
    // ========================

    [Fact]
    public void StartRpcClientActivity_CreatesClientActivity_WithAllSemanticTags()
    {
        // Act
        using var activity = RabbitXActivitySource.StartRpcClientActivity(
            "rpc.exchange", "rpc.orders", "OrderRpcClient", "corr-789");

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Client);
        activity.DisplayName.Should().Be("rpc.exchange publish");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingSystemKey).Should().Be("rabbitmq");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingDestinationNameKey).Should().Be("rpc.exchange");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingOperationTypeKey).Should().Be("publish");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingRabbitMQRoutingKeyKey).Should().Be("rpc.orders");
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXRpcClientNameKey).Should().Be("OrderRpcClient");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingMessageIdKey).Should().Be("corr-789");
    }

    [Fact]
    public void StartRpcClientActivity_DoesNotIncludeHandlerOrConsumerTags()
    {
        // Act
        using var activity = RabbitXActivitySource.StartRpcClientActivity(
            "rpc.exchange", "rpc.key", "client1", "corr-1");

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem(RabbitXTelemetryConstants.RabbitXRpcHandlerNameKey).Should().BeNull();
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXConsumerNameKey).Should().BeNull();
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXPublisherNameKey).Should().BeNull();
    }

    // ========================
    // RPC Server Activity
    // ========================

    [Fact]
    public void StartRpcServerActivity_CreatesServerActivity_WithAllSemanticTags()
    {
        // Act
        using var activity = RabbitXActivitySource.StartRpcServerActivity(
            "rpc.queue", "OrderRpcHandler", "corr-abc");

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Server);
        activity.DisplayName.Should().Be("rpc.queue process");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingSystemKey).Should().Be("rabbitmq");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingDestinationNameKey).Should().Be("rpc.queue");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingOperationTypeKey).Should().Be("process");
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXRpcHandlerNameKey).Should().Be("OrderRpcHandler");
        activity.GetTagItem(RabbitXTelemetryConstants.MessagingMessageIdKey).Should().Be("corr-abc");
    }

    [Fact]
    public void StartRpcServerActivity_WithParentContext_InheritsTraceId()
    {
        // Arrange
        using var clientActivity = RabbitXActivitySource.StartRpcClientActivity(
            "rpc.exchange", "rpc.orders", "client", "corr-1");
        var parentContext = clientActivity!.Context;

        // Act
        using var serverActivity = RabbitXActivitySource.StartRpcServerActivity(
            "rpc.queue", "OrderRpcHandler", "corr-1", parentContext);

        // Assert
        serverActivity.Should().NotBeNull();
        serverActivity!.Kind.Should().Be(ActivityKind.Server);
        serverActivity.ParentId.Should().NotBeNullOrEmpty();
        serverActivity.TraceId.Should().Be(clientActivity.TraceId);
        serverActivity.SpanId.Should().NotBe(clientActivity.SpanId);
    }

    [Fact]
    public void StartRpcServerActivity_WithDefaultContext_HasNoParent()
    {
        // Act
        using var activity = RabbitXActivitySource.StartRpcServerActivity(
            "rpc.queue", "handler1", "corr-1", default);

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Server);
    }

    // ========================
    // RecordException
    // ========================

    [Fact]
    public void RecordException_SetsErrorStatus_WithCorrectDescription()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 50);
        var exception = new InvalidOperationException("Test error");

        // Act
        RabbitXActivitySource.RecordException(activity, exception);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("Test error");
    }

    [Fact]
    public void RecordException_AddsExceptionEvent_WithAllOTelFields()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 50);
        var exception = new InvalidOperationException("Test error");

        // Act
        RabbitXActivitySource.RecordException(activity, exception);

        // Assert
        activity!.Events.Should().ContainSingle(e => e.Name == "exception");
        var exceptionEvent = activity.Events.First(e => e.Name == "exception");

        // Verify all three OTel exception semantic convention fields
        exceptionEvent.Tags.Should().Contain(t =>
            t.Key == "exception.type" && (string)t.Value! == typeof(InvalidOperationException).FullName);
        exceptionEvent.Tags.Should().Contain(t =>
            t.Key == "exception.message" && (string)t.Value! == "Test error");
        exceptionEvent.Tags.Should().Contain(t =>
            t.Key == "exception.stacktrace" && ((string)t.Value!).Contains("InvalidOperationException"));
    }

    [Fact]
    public void RecordException_WithNestedException_IncludesInnerExceptionInStacktrace()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 50);
        var inner = new ArgumentException("inner problem");
        var outer = new InvalidOperationException("outer problem", inner);

        // Act
        RabbitXActivitySource.RecordException(activity, outer);

        // Assert
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("outer problem");

        var exceptionEvent = activity.Events.First(e => e.Name == "exception");
        var stacktrace = (string)exceptionEvent.Tags.First(t => t.Key == "exception.stacktrace").Value!;
        stacktrace.Should().Contain("ArgumentException");
        stacktrace.Should().Contain("inner problem");
    }

    [Fact]
    public void RecordException_WithNullActivity_DoesNotThrow()
    {
        // Act & Assert
        var act = () => RabbitXActivitySource.RecordException(null, new Exception("test"));
        act.Should().NotThrow();
    }

    // ========================
    // Activity Status
    // ========================

    [Fact]
    public void Activity_CanBeSetToOkStatus_AfterCreation()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);

        // Act
        activity!.SetStatus(ActivityStatusCode.Ok);

        // Assert
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public void Activity_StatusDefaultsToUnset_BeforeExplicitSet()
    {
        // Act
        using var activity = RabbitXActivitySource.StartPublishActivity(
            "test.exchange", "test.key", "test", "msg-1", 100);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Unset);
    }

    // ========================
    // Multiple Activities
    // ========================

    [Fact]
    public void MultipleActivities_AreIndependent_WithDifferentTraceIds()
    {
        // Act
        using var activity1 = RabbitXActivitySource.StartPublishActivity(
            "ex1", "key1", "pub1", "msg-1", 50);
        activity1?.Stop();

        using var activity2 = RabbitXActivitySource.StartPublishActivity(
            "ex2", "key2", "pub2", "msg-2", 100);

        // Assert
        activity1.Should().NotBeNull();
        activity2.Should().NotBeNull();
        activity1!.SpanId.Should().NotBe(activity2!.SpanId);
    }

    [Fact]
    public void CapturedActivities_AreTrackedByListener()
    {
        // Arrange - record count before creating our activities
        var countBefore = _capturedActivities.Count;

        // Act
        using var a1 = RabbitXActivitySource.StartPublishActivity("ex1", "k1", "p1", "m1", 10);
        using var a2 = RabbitXActivitySource.StartConsumeActivity("q1", "c1", "m2");
        using var a3 = RabbitXActivitySource.StartRpcClientActivity("ex2", "k2", "rpc1", "corr1");
        using var a4 = RabbitXActivitySource.StartRpcServerActivity("q2", "h1", "corr2");

        // Assert - listener captured at least 4 new activities from our calls
        var newActivities = _capturedActivities.Count - countBefore;
        newActivities.Should().BeGreaterThanOrEqualTo(4);
    }

    // ========================
    // Tags Can Be Added After Creation (used by Consumer for retry count and result)
    // ========================

    [Fact]
    public void ConsumeActivity_CanHaveRetryCountAndResultTagsAddedLater()
    {
        // Arrange
        using var activity = RabbitXActivitySource.StartConsumeActivity(
            "orders.queue", "OrderConsumer", "msg-1");

        // Act - simulate what consumer does
        activity!.SetTag(RabbitXTelemetryConstants.RabbitXRetryCountKey, 3);
        activity.SetTag(RabbitXTelemetryConstants.RabbitXConsumeResultKey, "ack");
        activity.SetStatus(ActivityStatusCode.Ok);

        // Assert
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXRetryCountKey).Should().Be(3);
        activity.GetTagItem(RabbitXTelemetryConstants.RabbitXConsumeResultKey).Should().Be("ack");
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    public void Dispose()
    {
        _listener.Dispose();
        foreach (var activity in _capturedActivities)
        {
            activity.Dispose();
        }
    }
}
