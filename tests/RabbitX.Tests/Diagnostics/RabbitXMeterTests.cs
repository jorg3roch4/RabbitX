using System.Diagnostics.Metrics;
using FluentAssertions;
using RabbitX.Diagnostics;
using Xunit;

namespace RabbitX.Tests.Diagnostics;

public class RabbitXMeterTests : IDisposable
{
    private readonly MeterListener _listener;
    private readonly Dictionary<string, List<MeasurementRecord>> _measurements = new();

    public RabbitXMeterTests()
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == RabbitXTelemetryConstants.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            RecordMeasurement(instrument.Name, measurement, tags);
        });

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            RecordMeasurement(instrument.Name, measurement, tags);
        });

        _listener.Start();
    }

    private void RecordMeasurement(string instrumentName, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (!_measurements.ContainsKey(instrumentName))
            _measurements[instrumentName] = new List<MeasurementRecord>();

        var tagDict = new Dictionary<string, object?>();
        foreach (var tag in tags)
            tagDict[tag.Key] = tag.Value;

        _measurements[instrumentName].Add(new MeasurementRecord(value, tagDict));
    }

    // ========================
    // Publishing Metrics
    // ========================

    [Fact]
    public void MessagesPublished_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.MessagesPublished.Add(1,
            new KeyValuePair<string, object?>("publisher", "OrderPublisher"),
            new KeyValuePair<string, object?>("exchange", "orders.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.published");
        var record = _measurements["rabbitx.messages.published"].Should().ContainSingle().Subject;
        record.Value.Should().Be(1L);
        record.Tags["publisher"].Should().Be("OrderPublisher");
        record.Tags["exchange"].Should().Be("orders.exchange");
    }

    [Fact]
    public void PublishErrors_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.PublishErrors.Add(1,
            new KeyValuePair<string, object?>("publisher", "FailPub"),
            new KeyValuePair<string, object?>("exchange", "fail.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.publish.errors");
        var record = _measurements["rabbitx.messages.publish.errors"].Should().ContainSingle().Subject;
        record.Value.Should().Be(1L);
        record.Tags["publisher"].Should().Be("FailPub");
        record.Tags["exchange"].Should().Be("fail.exchange");
    }

    [Fact]
    public void PublishDuration_RecordsHistogram_WithMilliseconds()
    {
        // Act
        RabbitXMeter.PublishDuration.Record(15.75,
            new KeyValuePair<string, object?>("publisher", "test"),
            new KeyValuePair<string, object?>("exchange", "test.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.publish.duration");
        _measurements["rabbitx.messages.publish.duration"].Should().ContainSingle()
            .Which.Value.Should().Be(15.75);
    }

    [Fact]
    public void PublishMessageSize_RecordsHistogram_WithBytes()
    {
        // Act
        RabbitXMeter.PublishMessageSize.Record(1024L,
            new KeyValuePair<string, object?>("publisher", "test"),
            new KeyValuePair<string, object?>("exchange", "test.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.publish.size");
        _measurements["rabbitx.messages.publish.size"].Should().ContainSingle()
            .Which.Value.Should().Be(1024L);
    }

    // ========================
    // Consuming Metrics
    // ========================

    [Fact]
    public void MessagesConsumed_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.MessagesConsumed.Add(1,
            new KeyValuePair<string, object?>("consumer", "OrderConsumer"),
            new KeyValuePair<string, object?>("queue", "orders.queue"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.consumed");
        var record = _measurements["rabbitx.messages.consumed"].Should().ContainSingle().Subject;
        record.Tags["consumer"].Should().Be("OrderConsumer");
        record.Tags["queue"].Should().Be("orders.queue");
    }

    [Fact]
    public void ConsumeResults_RecordsCounter_WithResultTag()
    {
        // Act
        RabbitXMeter.ConsumeResults.Add(1,
            new KeyValuePair<string, object?>("consumer", "test"),
            new KeyValuePair<string, object?>("queue", "test.queue"),
            new KeyValuePair<string, object?>("result", "ack"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.consume.results");
        var record = _measurements["rabbitx.messages.consume.results"].Should().ContainSingle().Subject;
        record.Tags["result"].Should().Be("ack");
    }

    [Fact]
    public void ConsumeDuration_RecordsHistogram_WithMilliseconds()
    {
        // Act
        RabbitXMeter.ConsumeDuration.Record(42.3,
            new KeyValuePair<string, object?>("consumer", "OrderConsumer"),
            new KeyValuePair<string, object?>("queue", "orders.queue"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.consume.duration");
        var record = _measurements["rabbitx.messages.consume.duration"].Should().ContainSingle().Subject;
        record.Value.Should().Be(42.3);
        record.Tags["consumer"].Should().Be("OrderConsumer");
        record.Tags["queue"].Should().Be("orders.queue");
    }

    [Fact]
    public void ConsumeErrors_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.ConsumeErrors.Add(1,
            new KeyValuePair<string, object?>("consumer", "ErrorConsumer"),
            new KeyValuePair<string, object?>("queue", "error.queue"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.consume.errors");
        var record = _measurements["rabbitx.messages.consume.errors"].Should().ContainSingle().Subject;
        record.Tags["consumer"].Should().Be("ErrorConsumer");
        record.Tags["queue"].Should().Be("error.queue");
    }

    // ========================
    // Retry Metrics
    // ========================

    [Fact]
    public void RetryAttempts_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.RetryAttempts.Add(1,
            new KeyValuePair<string, object?>("consumer", "RetryConsumer"),
            new KeyValuePair<string, object?>("queue", "retry.queue"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.retries");
        var record = _measurements["rabbitx.messages.retries"].Should().ContainSingle().Subject;
        record.Tags["consumer"].Should().Be("RetryConsumer");
    }

    [Fact]
    public void RetryExhausted_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.RetryExhausted.Add(1,
            new KeyValuePair<string, object?>("consumer", "ExhaustedConsumer"),
            new KeyValuePair<string, object?>("queue", "exhaust.queue"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.retries.exhausted");
        var record = _measurements["rabbitx.messages.retries.exhausted"].Should().ContainSingle().Subject;
        record.Tags["consumer"].Should().Be("ExhaustedConsumer");
    }

    // ========================
    // RPC Metrics
    // ========================

    [Fact]
    public void RpcCallsMade_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.RpcCallsMade.Add(1,
            new KeyValuePair<string, object?>("client", "OrderRpc"),
            new KeyValuePair<string, object?>("exchange", "rpc.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.rpc.calls");
        var record = _measurements["rabbitx.rpc.calls"].Should().ContainSingle().Subject;
        record.Tags["client"].Should().Be("OrderRpc");
        record.Tags["exchange"].Should().Be("rpc.exchange");
    }

    [Fact]
    public void RpcDuration_RecordsHistogram_WithMilliseconds()
    {
        // Act
        RabbitXMeter.RpcDuration.Record(250.5,
            new KeyValuePair<string, object?>("client", "OrderRpc"),
            new KeyValuePair<string, object?>("exchange", "rpc.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.rpc.duration");
        var record = _measurements["rabbitx.rpc.duration"].Should().ContainSingle().Subject;
        record.Value.Should().Be(250.5);
        record.Tags["client"].Should().Be("OrderRpc");
    }

    [Fact]
    public void RpcTimeouts_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.RpcTimeouts.Add(1,
            new KeyValuePair<string, object?>("client", "TimeoutRpc"),
            new KeyValuePair<string, object?>("exchange", "rpc.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.rpc.timeouts");
        var record = _measurements["rabbitx.rpc.timeouts"].Should().ContainSingle().Subject;
        record.Tags["client"].Should().Be("TimeoutRpc");
    }

    [Fact]
    public void RpcErrors_RecordsCounter_WithCorrectTags()
    {
        // Act
        RabbitXMeter.RpcErrors.Add(1,
            new KeyValuePair<string, object?>("client", "ErrorRpc"),
            new KeyValuePair<string, object?>("exchange", "rpc.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.rpc.errors");
        var record = _measurements["rabbitx.rpc.errors"].Should().ContainSingle().Subject;
        record.Tags["client"].Should().Be("ErrorRpc");
    }

    // ========================
    // Connection Metrics
    // ========================

    [Fact]
    public void ConnectionsCreated_RecordsCounter_WithHostAndVhostTags()
    {
        // Act
        RabbitXMeter.ConnectionsCreated.Add(1,
            new KeyValuePair<string, object?>("host", "rabbitmq-prod"),
            new KeyValuePair<string, object?>("vhost", "/orders"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.connections.created");
        var record = _measurements["rabbitx.connections.created"].Should().ContainSingle().Subject;
        record.Tags["host"].Should().Be("rabbitmq-prod");
        record.Tags["vhost"].Should().Be("/orders");
    }

    [Fact]
    public void ConnectionErrors_RecordsCounter_WithHostAndVhostTags()
    {
        // Act
        RabbitXMeter.ConnectionErrors.Add(1,
            new KeyValuePair<string, object?>("host", "rabbitmq-down"),
            new KeyValuePair<string, object?>("vhost", "/"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.connections.errors");
        var record = _measurements["rabbitx.connections.errors"].Should().ContainSingle().Subject;
        record.Tags["host"].Should().Be("rabbitmq-down");
        record.Tags["vhost"].Should().Be("/");
    }

    // ========================
    // Accumulation
    // ========================

    [Fact]
    public void Counters_AccumulateMultipleMeasurements()
    {
        // Act
        RabbitXMeter.MessagesPublished.Add(1,
            new KeyValuePair<string, object?>("publisher", "test"),
            new KeyValuePair<string, object?>("exchange", "test.exchange"));
        RabbitXMeter.MessagesPublished.Add(1,
            new KeyValuePair<string, object?>("publisher", "test"),
            new KeyValuePair<string, object?>("exchange", "test.exchange"));
        RabbitXMeter.MessagesPublished.Add(1,
            new KeyValuePair<string, object?>("publisher", "test"),
            new KeyValuePair<string, object?>("exchange", "test.exchange"));

        _listener.RecordObservableInstruments();

        // Assert - 3 separate measurement events captured
        _measurements.Should().ContainKey("rabbitx.messages.published");
        _measurements["rabbitx.messages.published"].Should().HaveCount(3);
    }

    [Fact]
    public void Histograms_RecordMultipleMeasurements()
    {
        // Act
        RabbitXMeter.PublishDuration.Record(10.0,
            new KeyValuePair<string, object?>("publisher", "test"),
            new KeyValuePair<string, object?>("exchange", "test.exchange"));
        RabbitXMeter.PublishDuration.Record(20.0,
            new KeyValuePair<string, object?>("publisher", "test"),
            new KeyValuePair<string, object?>("exchange", "test.exchange"));

        _listener.RecordObservableInstruments();

        // Assert
        _measurements.Should().ContainKey("rabbitx.messages.publish.duration");
        var records = _measurements["rabbitx.messages.publish.duration"];
        records.Should().HaveCount(2);
        records[0].Value.Should().Be(10.0);
        records[1].Value.Should().Be(20.0);
    }

    [Fact]
    public void DifferentResultTags_AreRecordedSeparately()
    {
        // Act - simulate different consume results
        RabbitXMeter.ConsumeResults.Add(1,
            new KeyValuePair<string, object?>("consumer", "c1"),
            new KeyValuePair<string, object?>("queue", "q1"),
            new KeyValuePair<string, object?>("result", "ack"));
        RabbitXMeter.ConsumeResults.Add(1,
            new KeyValuePair<string, object?>("consumer", "c1"),
            new KeyValuePair<string, object?>("queue", "q1"),
            new KeyValuePair<string, object?>("result", "retry"));
        RabbitXMeter.ConsumeResults.Add(1,
            new KeyValuePair<string, object?>("consumer", "c1"),
            new KeyValuePair<string, object?>("queue", "q1"),
            new KeyValuePair<string, object?>("result", "reject"));

        _listener.RecordObservableInstruments();

        // Assert
        var records = _measurements["rabbitx.messages.consume.results"];
        records.Should().HaveCount(3);
        records.Select(r => r.Tags["result"]).Should().BeEquivalentTo(new[] { "ack", "retry", "reject" });
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private record MeasurementRecord(object Value, Dictionary<string, object?> Tags);
}
