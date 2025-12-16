using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitX.Interfaces;
using RabbitX.Models;
using RabbitX.Rpc;
using Xunit;

namespace RabbitX.Tests.Unit;

public class RpcResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        // Arrange
        var response = new TestResponse { Value = "test" };
        var correlationId = "corr-123";
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var result = RpcResult<TestResponse>.Ok(response, correlationId, duration);

        // Assert
        result.Success.Should().BeTrue();
        result.Response.Should().Be(response);
        result.CorrelationId.Should().Be(correlationId);
        result.Duration.Should().Be(duration);
        result.ErrorMessage.Should().BeNull();
        result.Exception.Should().BeNull();
        result.IsTimeout.Should().BeFalse();
    }

    [Fact]
    public void Timeout_CreatesTimeoutResult()
    {
        // Arrange
        var correlationId = "corr-456";
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var result = RpcResult<TestResponse>.Timeout(correlationId, timeout);

        // Assert
        result.Success.Should().BeFalse();
        result.Response.Should().BeNull();
        result.CorrelationId.Should().Be(correlationId);
        result.Duration.Should().Be(timeout);
        result.IsTimeout.Should().BeTrue();
        result.ErrorMessage.Should().Contain("timeout");
    }

    [Fact]
    public void Failed_CreatesFailedResult()
    {
        // Arrange
        var correlationId = "corr-789";
        var error = "Something went wrong";
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = RpcResult<TestResponse>.Failed(correlationId, error, exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Response.Should().BeNull();
        result.CorrelationId.Should().Be(correlationId);
        result.ErrorMessage.Should().Be(error);
        result.Exception.Should().Be(exception);
        result.IsTimeout.Should().BeFalse();
    }

    [Fact]
    public void FromException_CreatesResultFromException()
    {
        // Arrange
        var correlationId = "corr-abc";
        var exception = new TimeoutException("Request timed out");

        // Act
        var result = RpcResult<TestResponse>.FromException(correlationId, exception);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(exception.Message);
        result.Exception.Should().Be(exception);
    }

    private class TestResponse
    {
        public string Value { get; set; } = string.Empty;
    }
}

public class RpcCallOptionsTests
{
    [Fact]
    public void Default_HasDefaultValues()
    {
        // Act
        var options = RpcCallOptions.Default;

        // Assert
        options.Timeout.Should().BeNull();
        options.CorrelationId.Should().BeNull();
        options.Headers.Should().BeNull();
    }

    [Fact]
    public void WithTimeout_SetsTimeout()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(15);

        // Act
        var options = RpcCallOptions.WithTimeout(timeout);

        // Assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void WithCorrelationId_SetsCorrelationId()
    {
        // Arrange
        var correlationId = "my-corr-id";

        // Act
        var options = RpcCallOptions.WithCorrelationId(correlationId);

        // Assert
        options.CorrelationId.Should().Be(correlationId);
    }
}

public class PendingRpcRequestTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var responseType = typeof(string);
        var tcs = new TaskCompletionSource<string>();
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var request = new PendingRpcRequest(responseType, tcs, timeout);

        // Assert
        request.ResponseType.Should().Be(responseType);
        request.TaskCompletionSource.Should().Be(tcs);
        request.Timeout.Should().Be(timeout);
        request.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenNotExpired()
    {
        // Arrange
        var request = new PendingRpcRequest(typeof(string), new TaskCompletionSource<string>(), TimeSpan.FromMinutes(5));

        // Assert
        request.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task Complete_SetsTaskResult()
    {
        // Arrange
        var tcs = new TaskCompletionSource<string>();
        var request = new PendingRpcRequest(typeof(string), tcs, TimeSpan.FromSeconds(30));

        // Act
        request.Complete("test result");

        // Assert
        var result = await tcs.Task;
        result.Should().Be("test result");
    }

    [Fact]
    public async Task Fail_SetsTaskException()
    {
        // Arrange
        var tcs = new TaskCompletionSource<string>();
        var request = new PendingRpcRequest(typeof(string), tcs, TimeSpan.FromSeconds(30));
        var exception = new InvalidOperationException("Test error");

        // Act
        request.Fail(exception);

        // Assert
        var act = async () => await tcs.Task;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test error");
    }

    [Fact]
    public void Cancel_CancelsTask()
    {
        // Arrange
        var tcs = new TaskCompletionSource<string>();
        var request = new PendingRpcRequest(typeof(string), tcs, TimeSpan.FromSeconds(30));

        // Act
        request.Cancel();

        // Assert
        tcs.Task.IsCanceled.Should().BeTrue();
    }
}

public class RpcPendingRequestsTests
{
    private readonly Mock<IMessageSerializer> _serializerMock;
    private readonly Mock<ILogger<RpcPendingRequests>> _loggerMock;

    public RpcPendingRequestsTests()
    {
        _serializerMock = new Mock<IMessageSerializer>();
        _loggerMock = new Mock<ILogger<RpcPendingRequests>>();
    }

    [Fact]
    public void Register_AddsPendingRequest()
    {
        // Arrange
        using var pendingRequests = new RpcPendingRequests(_serializerMock.Object, _loggerMock.Object);
        var tcs = new TaskCompletionSource<string>();

        // Act
        pendingRequests.Register("corr-1", tcs, TimeSpan.FromSeconds(30));

        // Assert
        pendingRequests.PendingCount.Should().Be(1);
        pendingRequests.TryGet("corr-1", out var pending).Should().BeTrue();
        pending.Should().NotBeNull();
    }

    [Fact]
    public void TryComplete_CompletesAndRemovesRequest()
    {
        // Arrange
        using var pendingRequests = new RpcPendingRequests(_serializerMock.Object, _loggerMock.Object);
        var tcs = new TaskCompletionSource<string>();
        pendingRequests.Register("corr-1", tcs, TimeSpan.FromSeconds(30));

        var responseBytes = System.Text.Encoding.UTF8.GetBytes("\"test response\"");
        _serializerMock
            .Setup(s => s.Deserialize(It.IsAny<ReadOnlyMemory<byte>>(), typeof(string)))
            .Returns("test response");

        // Act
        var completed = pendingRequests.TryComplete("corr-1", responseBytes);

        // Assert
        completed.Should().BeTrue();
        pendingRequests.PendingCount.Should().Be(0);
        tcs.Task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void TryComplete_ReturnsFalse_WhenNotFound()
    {
        // Arrange
        using var pendingRequests = new RpcPendingRequests(_serializerMock.Object, _loggerMock.Object);
        var responseBytes = System.Text.Encoding.UTF8.GetBytes("\"test\"");

        // Act
        var completed = pendingRequests.TryComplete("unknown", responseBytes);

        // Assert
        completed.Should().BeFalse();
    }

    [Fact]
    public void TryFail_FailsAndRemovesRequest()
    {
        // Arrange
        using var pendingRequests = new RpcPendingRequests(_serializerMock.Object, _loggerMock.Object);
        var tcs = new TaskCompletionSource<string>();
        pendingRequests.Register("corr-1", tcs, TimeSpan.FromSeconds(30));
        var exception = new InvalidOperationException("Error");

        // Act
        var failed = pendingRequests.TryFail("corr-1", exception);

        // Assert
        failed.Should().BeTrue();
        pendingRequests.PendingCount.Should().Be(0);
        tcs.Task.IsFaulted.Should().BeTrue();
    }

    [Fact]
    public void TryRemove_RemovesRequest()
    {
        // Arrange
        using var pendingRequests = new RpcPendingRequests(_serializerMock.Object, _loggerMock.Object);
        var tcs = new TaskCompletionSource<string>();
        pendingRequests.Register("corr-1", tcs, TimeSpan.FromSeconds(30));

        // Act
        var removed = pendingRequests.TryRemove("corr-1");

        // Assert
        removed.Should().BeTrue();
        pendingRequests.PendingCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_CancelsAllPendingRequests()
    {
        // Arrange
        var pendingRequests = new RpcPendingRequests(_serializerMock.Object, _loggerMock.Object);
        var tcs1 = new TaskCompletionSource<string>();
        var tcs2 = new TaskCompletionSource<string>();
        pendingRequests.Register("corr-1", tcs1, TimeSpan.FromSeconds(30));
        pendingRequests.Register("corr-2", tcs2, TimeSpan.FromSeconds(30));

        // Act
        pendingRequests.Dispose();

        // Assert
        tcs1.Task.IsCanceled.Should().BeTrue();
        tcs2.Task.IsCanceled.Should().BeTrue();
    }
}

public class RpcContextTests
{
    [Fact]
    public void RpcContext_HasRequiredProperties()
    {
        // Arrange & Act
        var context = new RpcContext<TestRequest>
        {
            Message = new TestRequest { Value = "test" },
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            ReplyTo = "reply.queue"
        };

        // Assert
        context.Message.Value.Should().Be("test");
        context.MessageId.Should().Be("msg-123");
        context.CorrelationId.Should().Be("corr-456");
        context.ReplyTo.Should().Be("reply.queue");
        context.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    private class TestRequest
    {
        public string Value { get; set; } = string.Empty;
    }
}
