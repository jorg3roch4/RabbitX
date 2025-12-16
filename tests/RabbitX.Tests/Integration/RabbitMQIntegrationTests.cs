using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitX.Configuration;
using RabbitX.Extensions;
using RabbitX.Interfaces;
using RabbitX.Models;
using Xunit;
using Xunit.Abstractions;

namespace RabbitX.Tests.Integration;

/// <summary>
/// Integration tests for RabbitMQ functionality.
/// Requires a running RabbitMQ instance at localhost:5672 with rabbit/rabbit credentials.
/// </summary>
public class RabbitMQIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public RabbitMQIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private ServiceProvider CreateServiceProvider(string testId)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddDebug()
            .SetMinimumLevel(LogLevel.Debug));

        services.AddRabbitX(options => options
            .UseConnection("localhost", 5672)
            .UseCredentials("rabbit", "rabbit")
            .UseVirtualHost("/")
            .WithClientName($"RabbitX.IntegrationTests.{testId}")
            .AddPublisher($"TestPublisher_{testId}", pub => pub
                .ToExchange($"test.exchange.{testId}", "direct")
                .WithRoutingKey($"test.message.{testId}")
                .Durable()
                .Persistent())
            .AddConsumer($"TestConsumer_{testId}", con => con
                .FromQueue($"test.queue.{testId}")
                .BindToExchange($"test.exchange.{testId}", $"test.message.{testId}")
                .WithPrefetch(1)
                .WithRetry(r => r.MaxRetries(3).ThenDiscard())));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PublishAndConsume_SingleMessage_Success()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        await using var serviceProvider = CreateServiceProvider(testId);

        var receivedMessages = new List<TestMessage>();
        var messageReceivedSignal = new SemaphoreSlim(0);

        var publisherFactory = serviceProvider.GetRequiredService<IPublisherFactory>();
        var consumerFactory = serviceProvider.GetRequiredService<IConsumerFactory>();

        var publisher = publisherFactory.CreatePublisher<TestMessage>($"TestPublisher_{testId}");

        var handler = new TestMessageHandler(msg =>
        {
            receivedMessages.Add(msg);
            messageReceivedSignal.Release();
            return ConsumeResult.Ack;
        });

        var consumer = consumerFactory.CreateConsumer<TestMessage>($"TestConsumer_{testId}", handler);

        var testMessage = new TestMessage
        {
            Id = Guid.NewGuid().ToString(),
            Content = "Hello RabbitX!",
            Timestamp = DateTime.UtcNow
        };

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Start consumer
        await consumer.StartAsync(cts.Token);

        _output.WriteLine($"Publishing message: {testMessage.Id}");
        await publisher.PublishAsync(testMessage, cts.Token);

        // Wait for message
        var received = await messageReceivedSignal.WaitAsync(TimeSpan.FromSeconds(10));

        await consumer.StopAsync(cts.Token);

        // Assert
        received.Should().BeTrue("message should be received within timeout");
        receivedMessages.Should().ContainSingle();
        receivedMessages[0].Id.Should().Be(testMessage.Id);
        receivedMessages[0].Content.Should().Be(testMessage.Content);

        _output.WriteLine($"Message received successfully: {receivedMessages[0].Id}");
    }

    [Fact]
    public async Task PublishWithConfirm_SingleMessage_ReturnsSuccess()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        await using var serviceProvider = CreateServiceProvider(testId);

        var publisherFactory = serviceProvider.GetRequiredService<IPublisherFactory>();
        var publisher = publisherFactory.CreateReliablePublisher<TestMessage>($"TestPublisher_{testId}");

        var testMessage = new TestMessage
        {
            Id = Guid.NewGuid().ToString(),
            Content = "Confirmed message",
            Timestamp = DateTime.UtcNow
        };

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        _output.WriteLine($"Publishing message with confirm: {testMessage.Id}");
        var result = await publisher.PublishWithConfirmAsync(
            testMessage,
            new PublishOptions { CorrelationId = testMessage.Id },
            cts.Token);

        // Assert
        result.Success.Should().BeTrue();
        _output.WriteLine($"Message confirmed by broker. MessageId: {result.MessageId}");
    }

    [Fact]
    public async Task PublishMultipleMessages_AllReceived()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        await using var serviceProvider = CreateServiceProvider(testId);

        var receivedMessages = new List<TestMessage>();
        var messageReceivedSignal = new SemaphoreSlim(0);

        var publisherFactory = serviceProvider.GetRequiredService<IPublisherFactory>();
        var consumerFactory = serviceProvider.GetRequiredService<IConsumerFactory>();

        var publisher = publisherFactory.CreatePublisher<TestMessage>($"TestPublisher_{testId}");

        var handler = new TestMessageHandler(msg =>
        {
            receivedMessages.Add(msg);
            messageReceivedSignal.Release();
            return ConsumeResult.Ack;
        });

        var consumer = consumerFactory.CreateConsumer<TestMessage>($"TestConsumer_{testId}", handler);

        var messageCount = 5;
        var testMessages = Enumerable.Range(1, messageCount)
            .Select(i => new TestMessage
            {
                Id = $"MSG-{i:D3}",
                Content = $"Message {i}",
                Timestamp = DateTime.UtcNow
            })
            .ToList();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await consumer.StartAsync(cts.Token);

        foreach (var msg in testMessages)
        {
            _output.WriteLine($"Publishing: {msg.Id}");
            await publisher.PublishAsync(msg, cts.Token);
        }

        // Wait for all messages
        for (int i = 0; i < messageCount; i++)
        {
            var received = await messageReceivedSignal.WaitAsync(TimeSpan.FromSeconds(10));
            if (!received) break;
        }

        await consumer.StopAsync(cts.Token);

        // Assert
        receivedMessages.Should().HaveCount(messageCount);
        _output.WriteLine($"All {messageCount} messages received successfully");
    }
}

public class TestMessage
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class TestMessageHandler : IMessageHandler<TestMessage>
{
    private readonly Func<TestMessage, ConsumeResult> _handler;

    public TestMessageHandler(Func<TestMessage, ConsumeResult> handler)
    {
        _handler = handler;
    }

    public Task<ConsumeResult> HandleAsync(
        MessageContext<TestMessage> context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_handler(context.Message));
    }
}
