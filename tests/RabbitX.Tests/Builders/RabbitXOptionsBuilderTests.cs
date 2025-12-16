using System.Reflection;
using FluentAssertions;
using RabbitX.Builders;
using RabbitX.Configuration;
using Xunit;

namespace RabbitX.Tests.Builders;

public class RabbitXOptionsBuilderTests
{
    [Fact]
    public void UseConnection_SetsHostAndPort()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("myhost", 5673)
            .UseCredentials("user", "pass")
            .AddPublisher("Test", p => p.ToExchange("test.exchange"));

        var options = GetOptions(builder);

        // Assert
        options.Connection.HostName.Should().Be("myhost");
        options.Connection.Port.Should().Be(5673);
    }

    [Fact]
    public void UseCredentials_SetsUserNameAndPassword()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .UseCredentials("testuser", "testpass")
            .AddPublisher("Test", p => p.ToExchange("test.exchange"));

        var options = GetOptions(builder);

        // Assert
        options.Connection.UserName.Should().Be("testuser");
        options.Connection.Password.Should().Be("testpass");
    }

    [Fact]
    public void AddPublisher_AddsPublisherConfiguration()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddPublisher("PaymentPublisher", pub => pub
                .ToExchange("payments.exchange", "direct")
                .WithRoutingKey("payment.process")
                .Durable()
                .Persistent());

        var options = GetOptions(builder);

        // Assert
        options.Publishers.Should().ContainKey("PaymentPublisher");
        var publisher = options.Publishers["PaymentPublisher"];
        publisher.Exchange.Should().Be("payments.exchange");
        publisher.ExchangeType.Should().Be("direct");
        publisher.RoutingKey.Should().Be("payment.process");
        publisher.Durable.Should().BeTrue();
        publisher.Persistent.Should().BeTrue();
    }

    [Fact]
    public void AddConsumer_AddsConsumerConfiguration()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("PaymentConsumer", con => con
                .FromQueue("payments.queue")
                .BindToExchange("payments.exchange", "payment.process")
                .WithPrefetch(10)
                .WithRetry(r => r.MaxRetries(5).ThenDiscard())); // ThenDiscard to avoid DLX requirement

        var options = GetOptions(builder);

        // Assert
        options.Consumers.Should().ContainKey("PaymentConsumer");
        var consumer = options.Consumers["PaymentConsumer"];
        consumer.Queue.Should().Be("payments.queue");
        consumer.Exchange.Should().Be("payments.exchange");
        consumer.RoutingKey.Should().Be("payment.process");
        consumer.Qos.PrefetchCount.Should().Be(10);
        consumer.Retry.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void AddConsumer_WithDeadLetter_ConfiguresDeadLetter()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("TestConsumer", con => con
                .FromQueue("test.queue")
                .WithDeadLetter(dlx => dlx
                    .Exchange("test.dlx")
                    .RoutingKey("test.failed")
                    .Queue("test.dlq")));

        var options = GetOptions(builder);

        // Assert
        var consumer = options.Consumers["TestConsumer"];
        consumer.DeadLetter.Should().NotBeNull();
        consumer.DeadLetter!.Exchange.Should().Be("test.dlx");
        consumer.DeadLetter.RoutingKey.Should().Be("test.failed");
        consumer.DeadLetter.Queue.Should().Be("test.dlq");
    }

    [Fact]
    public void AddConsumer_WithRetryDelays_ConfiguresSpecificDelays()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("TestConsumer", con => con
                .FromQueue("test.queue")
                .WithRetry(retry => retry
                    .WithDelays(
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromSeconds(60))
                    .ThenDiscard()));

        var options = GetOptions(builder);

        // Assert
        var consumer = options.Consumers["TestConsumer"];
        consumer.Retry.MaxRetries.Should().Be(3);
        consumer.Retry.DelaysInSeconds.Should().BeEquivalentTo(new[] { 10, 30, 60 });
        consumer.Retry.OnRetryExhausted.Should().Be(RetryExhaustedAction.Discard);
    }

    [Fact]
    public void AddConsumer_WithExponentialBackoff_ConfiguresBackoff()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("TestConsumer", con => con
                .FromQueue("test.queue")
                .WithRetry(retry => retry
                    .MaxRetries(5)
                    .ExponentialBackoff(
                        initialDelay: TimeSpan.FromSeconds(5),
                        maxDelay: TimeSpan.FromMinutes(5),
                        multiplier: 2.0)
                    .WithJitter()
                    .ThenDiscard())); // ThenDiscard to avoid DLX requirement

        var options = GetOptions(builder);

        // Assert
        var consumer = options.Consumers["TestConsumer"];
        consumer.Retry.MaxRetries.Should().Be(5);
        consumer.Retry.InitialDelaySeconds.Should().Be(5);
        consumer.Retry.MaxDelaySeconds.Should().Be(300);
        consumer.Retry.BackoffMultiplier.Should().Be(2.0);
        consumer.Retry.UseJitter.Should().BeTrue();
    }

    [Fact]
    public void Build_WithoutHostName_ThrowsException()
    {
        // Arrange
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("", 5672);

        // Act & Assert
        var exception = Assert.Throws<TargetInvocationException>(() => GetOptions(builder));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.InnerException!.Message.Should().Contain("hostname");
    }

    [Fact]
    public void Build_PublisherWithoutExchange_ThrowsException()
    {
        // Arrange
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddPublisher("BadPublisher", pub => pub.WithRoutingKey("test"));

        // Act & Assert
        var exception = Assert.Throws<TargetInvocationException>(() => GetOptions(builder));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.InnerException!.Message.Should().Contain("exchange");
    }

    [Fact]
    public void Build_ConsumerWithDLXActionButNoDLX_ThrowsException()
    {
        // Arrange
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("BadConsumer", con => con
                .FromQueue("test.queue")
                .WithRetry(r => r.ThenSendToDeadLetter()));

        // Act & Assert
        var exception = Assert.Throws<TargetInvocationException>(() => GetOptions(builder));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.InnerException!.Message.Should().Contain("dead letter");
    }

    private static RabbitXOptions GetOptions(RabbitXOptionsBuilder builder)
    {
        // Use reflection to access internal Build method
        var method = typeof(RabbitXOptionsBuilder).GetMethod(
            "Build",
            BindingFlags.NonPublic | BindingFlags.Instance);

        return (RabbitXOptions)method!.Invoke(builder, null)!;
    }
}
