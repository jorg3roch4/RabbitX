using FluentAssertions;
using RabbitX.Configuration;
using Xunit;

namespace RabbitX.Tests.Resilience;

public class RetryOptionsTests
{
    [Fact]
    public void GetDelayForAttempt_WithSpecificDelays_ReturnsCorrectDelay()
    {
        // Arrange
        var options = new RetryOptions
        {
            DelaysInSeconds = new[] { 10, 30, 60, 120, 300 }
        };

        // Act & Assert
        options.GetDelayForAttempt(1).Should().Be(TimeSpan.FromSeconds(10));
        options.GetDelayForAttempt(2).Should().Be(TimeSpan.FromSeconds(30));
        options.GetDelayForAttempt(3).Should().Be(TimeSpan.FromSeconds(60));
        options.GetDelayForAttempt(4).Should().Be(TimeSpan.FromSeconds(120));
        options.GetDelayForAttempt(5).Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public void GetDelayForAttempt_WithSpecificDelays_UsesLastDelayForExcessAttempts()
    {
        // Arrange
        var options = new RetryOptions
        {
            DelaysInSeconds = new[] { 10, 30 }
        };

        // Act & Assert
        options.GetDelayForAttempt(3).Should().Be(TimeSpan.FromSeconds(30));
        options.GetDelayForAttempt(10).Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetDelayForAttempt_WithExponentialBackoff_CalculatesCorrectly()
    {
        // Arrange
        var options = new RetryOptions
        {
            InitialDelaySeconds = 5,
            BackoffMultiplier = 2.0,
            MaxDelaySeconds = 300
        };

        // Act & Assert
        options.GetDelayForAttempt(1).Should().Be(TimeSpan.FromSeconds(5));   // 5 * 2^0 = 5
        options.GetDelayForAttempt(2).Should().Be(TimeSpan.FromSeconds(10));  // 5 * 2^1 = 10
        options.GetDelayForAttempt(3).Should().Be(TimeSpan.FromSeconds(20));  // 5 * 2^2 = 20
        options.GetDelayForAttempt(4).Should().Be(TimeSpan.FromSeconds(40));  // 5 * 2^3 = 40
        options.GetDelayForAttempt(5).Should().Be(TimeSpan.FromSeconds(80));  // 5 * 2^4 = 80
    }

    [Fact]
    public void GetDelayForAttempt_WithExponentialBackoff_CapsAtMaxDelay()
    {
        // Arrange
        var options = new RetryOptions
        {
            InitialDelaySeconds = 10,
            BackoffMultiplier = 3.0,
            MaxDelaySeconds = 60
        };

        // Act
        var delay = options.GetDelayForAttempt(5); // 10 * 3^4 = 810, but capped at 60

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void GetDelayForAttempt_WithZeroAttempt_ReturnsZero()
    {
        // Arrange
        var options = new RetryOptions
        {
            DelaysInSeconds = new[] { 10, 30 }
        };

        // Act & Assert
        options.GetDelayForAttempt(0).Should().Be(TimeSpan.Zero);
        options.GetDelayForAttempt(-1).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void IsEnabled_WithZeroMaxRetries_ReturnsFalse()
    {
        // Arrange
        var options = new RetryOptions { MaxRetries = 0 };

        // Assert
        options.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WithPositiveMaxRetries_ReturnsTrue()
    {
        // Arrange
        var options = new RetryOptions { MaxRetries = 3 };

        // Assert
        options.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var options = new RetryOptions();

        // Assert
        options.MaxRetries.Should().Be(3);
        options.InitialDelaySeconds.Should().Be(5);
        options.MaxDelaySeconds.Should().Be(300);
        options.BackoffMultiplier.Should().Be(2.0);
        options.UseJitter.Should().BeTrue();
        options.OnRetryExhausted.Should().Be(RetryExhaustedAction.SendToDeadLetter);
    }
}
