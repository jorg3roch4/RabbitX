using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.HealthChecks;
using Xunit;

namespace RabbitX.Tests.HealthChecks;

public class RabbitMQHealthCheckTests
{
    private readonly Mock<IRabbitMQConnection> _connectionMock;
    private readonly Mock<ILogger<RabbitMQHealthCheck>> _loggerMock;
    private readonly RabbitXOptions _options;
    private readonly RabbitMQHealthCheck _healthCheck;

    public RabbitMQHealthCheckTests()
    {
        _connectionMock = new Mock<IRabbitMQConnection>();
        _loggerMock = new Mock<ILogger<RabbitMQHealthCheck>>();
        _options = new RabbitXOptions
        {
            Connection = new ConnectionOptions
            {
                HostName = "localhost",
                Port = 5672,
                VirtualHost = "/"
            }
        };
        _healthCheck = new RabbitMQHealthCheck(_connectionMock.Object, _options, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNotConnected_ReturnsUnhealthy()
    {
        // Arrange
        _connectionMock.Setup(c => c.IsConnected).Returns(false);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("rabbitmq", _healthCheck, null, null)
        };

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not open");
        result.Data.Should().ContainKey("host");
        result.Data["host"].Should().Be("localhost");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenBlocked_ReturnsDegraded()
    {
        // Arrange
        _connectionMock.Setup(c => c.IsConnected).Returns(true);
        _connectionMock.Setup(c => c.IsBlocked).Returns(true);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("rabbitmq", _healthCheck, null, null)
        };

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("blocked");
        result.Data.Should().ContainKey("isBlocked");
        result.Data["isBlocked"].Should().Be(true);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHealthy_ReturnsHealthy()
    {
        // Arrange
        _connectionMock.Setup(c => c.IsConnected).Returns(true);
        _connectionMock.Setup(c => c.IsBlocked).Returns(false);

        var channelMock = new Mock<IChannel>();
        _connectionMock
            .Setup(c => c.CreateChannelAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        var rabbitConnectionMock = new Mock<IConnection>();
        rabbitConnectionMock.Setup(c => c.ClientProvidedName).Returns("test-client");
        rabbitConnectionMock.Setup(c => c.ServerProperties).Returns(new Dictionary<string, object?>
        {
            ["product"] = System.Text.Encoding.UTF8.GetBytes("RabbitMQ"),
            ["version"] = System.Text.Encoding.UTF8.GetBytes("3.12.0")
        });

        _connectionMock
            .Setup(c => c.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rabbitConnectionMock.Object);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("rabbitmq", _healthCheck, null, null)
        };

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("healthy");
        result.Data.Should().ContainKey("clientName");
        result.Data["clientName"].Should().Be("test-client");
        result.Data.Should().ContainKey("server");
        result.Data["server"].Should().Be("RabbitMQ");
        result.Data.Should().ContainKey("version");
        result.Data["version"].Should().Be("3.12.0");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCreateChannelThrows_ReturnsUnhealthy()
    {
        // Arrange
        _connectionMock.Setup(c => c.IsConnected).Returns(true);
        _connectionMock.Setup(c => c.IsBlocked).Returns(false);
        _connectionMock
            .Setup(c => c.CreateChannelAsync(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Channel creation failed"));

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("rabbitmq", _healthCheck, null, null)
        };

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Channel creation failed");
        result.Exception.Should().NotBeNull();
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCancelled_ReturnsUnhealthy()
    {
        // Arrange
        _connectionMock.Setup(c => c.IsConnected).Returns(true);
        _connectionMock.Setup(c => c.IsBlocked).Returns(false);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _connectionMock
            .Setup(c => c.CreateChannelAsync(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("rabbitmq", _healthCheck, null, null)
        };

        // Act
        var result = await _healthCheck.CheckHealthAsync(context, cts.Token);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("timed out");
    }

    [Fact]
    public async Task CheckHealthAsync_WithNullServerProperties_ReturnsHealthyWithoutServerInfo()
    {
        // Arrange
        _connectionMock.Setup(c => c.IsConnected).Returns(true);
        _connectionMock.Setup(c => c.IsBlocked).Returns(false);

        var channelMock = new Mock<IChannel>();
        _connectionMock
            .Setup(c => c.CreateChannelAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        var rabbitConnectionMock = new Mock<IConnection>();
        rabbitConnectionMock.Setup(c => c.ClientProvidedName).Returns("test-client");
        rabbitConnectionMock.Setup(c => c.ServerProperties).Returns((IDictionary<string, object?>)null!);

        _connectionMock
            .Setup(c => c.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rabbitConnectionMock.Object);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("rabbitmq", _healthCheck, null, null)
        };

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().NotContainKey("server");
        result.Data.Should().NotContainKey("version");
    }
}

public class RabbitMQHealthCheckOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Act
        var options = new RabbitMQHealthCheckOptions();

        // Assert
        options.Name.Should().Be("rabbitmq");
        options.Tags.Should().BeNull();
        options.FailureStatus.Should().BeNull();
        options.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CanSetAllProperties()
    {
        // Arrange & Act
        var options = new RabbitMQHealthCheckOptions
        {
            Name = "custom-rabbitmq",
            Tags = new[] { "ready", "messaging" },
            FailureStatus = HealthStatus.Degraded,
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Assert
        options.Name.Should().Be("custom-rabbitmq");
        options.Tags.Should().BeEquivalentTo(new[] { "ready", "messaging" });
        options.FailureStatus.Should().Be(HealthStatus.Degraded);
        options.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }
}
