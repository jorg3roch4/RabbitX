using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitX.Extensions;
using RabbitX.Rpc;
using Xunit;
using Xunit.Abstractions;

namespace RabbitX.Tests.Integration;

/// <summary>
/// Integration tests for RPC functionality.
/// Requires a running RabbitMQ instance at localhost:5672 with rabbit/rabbit credentials.
/// Run these tests with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class RpcIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public RpcIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RpcCall_SimpleRequest_ReturnsResponse()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        _output.WriteLine($"Test ID: {testId}");

        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddDebug()
            .SetMinimumLevel(LogLevel.Debug));

        services.AddRabbitX(options => options
            .UseConnection("localhost", 5672)
            .UseCredentials("rabbit", "rabbit")
            .UseVirtualHost("/")
            .WithClientName($"RabbitX.RpcTest.{testId}")
            .AddRpcClient<EchoRequest, EchoResponse>($"EchoClient_{testId}", rpc => rpc
                .ToExchange($"rpc.test.exchange.{testId}", "direct")
                .WithRoutingKey($"rpc.echo.{testId}")
                .WithTimeout(TimeSpan.FromSeconds(10))
                .UseDirectReplyTo())
            .AddRpcHandler<EchoRequest, EchoResponse, EchoHandler>($"EchoHandler_{testId}", handler => handler
                .FromQueue($"rpc.echo.queue.{testId}")
                .BindToExchange($"rpc.test.exchange.{testId}", $"rpc.echo.{testId}")
                .WithPrefetchCount(10)));

        await using var serviceProvider = services.BuildServiceProvider();

        // Start the RPC handler hosted service
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        var rpcService = hostedServices.OfType<RpcConsumerHostedService>().FirstOrDefault();
        rpcService.Should().NotBeNull("RpcConsumerHostedService should be registered");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Start the hosted service
        await rpcService!.StartAsync(cts.Token);
        await Task.Delay(500, cts.Token); // Give the handler time to start

        // Act
        var clientFactory = serviceProvider.GetRequiredService<IRpcClientFactory>();
        var client = clientFactory.CreateClient<EchoRequest, EchoResponse>($"EchoClient_{testId}");

        var request = new EchoRequest
        {
            Message = "Hello RPC!",
            Timestamp = DateTimeOffset.UtcNow
        };

        _output.WriteLine($"Sending RPC request: {request.Message}");
        var response = await client.CallAsync(request, cts.Token);

        // Assert
        response.Should().NotBeNull();
        response.EchoedMessage.Should().Be($"Echo: {request.Message}");
        response.ProcessedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        _output.WriteLine($"RPC response received: {response.EchoedMessage}");

        // Cleanup
        await rpcService.StopAsync(cts.Token);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task RpcCall_WithTimeout_ReturnsTimeoutResult()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        _output.WriteLine($"Test ID: {testId}");

        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddDebug()
            .SetMinimumLevel(LogLevel.Debug));

        services.AddRabbitX(options => options
            .UseConnection("localhost", 5672)
            .UseCredentials("rabbit", "rabbit")
            .UseVirtualHost("/")
            .WithClientName($"RabbitX.RpcTimeoutTest.{testId}")
            .AddRpcClient<EchoRequest, EchoResponse>($"TimeoutClient_{testId}", rpc => rpc
                .ToExchange($"rpc.timeout.exchange.{testId}", "direct")
                .WithRoutingKey($"rpc.timeout.{testId}")
                .WithTimeout(TimeSpan.FromSeconds(1))
                .UseDirectReplyTo()));
            // Note: No handler configured - request will timeout

        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        var clientFactory = serviceProvider.GetRequiredService<IRpcClientFactory>();
        var client = clientFactory.CreateClient<EchoRequest, EchoResponse>($"TimeoutClient_{testId}");

        var request = new EchoRequest
        {
            Message = "This will timeout",
            Timestamp = DateTimeOffset.UtcNow
        };

        _output.WriteLine("Sending RPC request that will timeout...");
        var result = await client.TryCallAsync(request, TimeSpan.FromSeconds(2));

        // Assert
        result.Success.Should().BeFalse();
        result.IsTimeout.Should().BeTrue();
        result.ErrorMessage.Should().Contain("timeout");

        _output.WriteLine($"RPC timeout as expected: {result.ErrorMessage}");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task RpcCall_MultipleRequests_AllSucceed()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        _output.WriteLine($"Test ID: {testId}");

        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddDebug()
            .SetMinimumLevel(LogLevel.Debug));

        services.AddRabbitX(options => options
            .UseConnection("localhost", 5672)
            .UseCredentials("rabbit", "rabbit")
            .UseVirtualHost("/")
            .WithClientName($"RabbitX.RpcMultiTest.{testId}")
            .AddRpcClient<EchoRequest, EchoResponse>($"MultiEchoClient_{testId}", rpc => rpc
                .ToExchange($"rpc.multi.exchange.{testId}", "direct")
                .WithRoutingKey($"rpc.multi.{testId}")
                .WithTimeout(TimeSpan.FromSeconds(10))
                .UseDirectReplyTo())
            .AddRpcHandler<EchoRequest, EchoResponse, EchoHandler>($"MultiEchoHandler_{testId}", handler => handler
                .FromQueue($"rpc.multi.queue.{testId}")
                .BindToExchange($"rpc.multi.exchange.{testId}", $"rpc.multi.{testId}")
                .WithPrefetchCount(10)));

        await using var serviceProvider = services.BuildServiceProvider();

        // Start the RPC handler hosted service
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        var rpcService = hostedServices.OfType<RpcConsumerHostedService>().FirstOrDefault();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await rpcService!.StartAsync(cts.Token);
        await Task.Delay(500, cts.Token);

        // Act
        var clientFactory = serviceProvider.GetRequiredService<IRpcClientFactory>();
        var client = clientFactory.CreateClient<EchoRequest, EchoResponse>($"MultiEchoClient_{testId}");

        var requestCount = 5;
        var tasks = new List<Task<EchoResponse>>();

        for (int i = 0; i < requestCount; i++)
        {
            var request = new EchoRequest
            {
                Message = $"Request {i + 1}",
                Timestamp = DateTimeOffset.UtcNow
            };

            _output.WriteLine($"Sending RPC request {i + 1}: {request.Message}");
            tasks.Add(client.CallAsync(request, cts.Token));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(requestCount);
        for (int i = 0; i < requestCount; i++)
        {
            responses[i].EchoedMessage.Should().Be($"Echo: Request {i + 1}");
            _output.WriteLine($"Response {i + 1} received: {responses[i].EchoedMessage}");
        }

        // Cleanup
        await rpcService.StopAsync(cts.Token);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task TryCallAsync_WithResult_ReturnsRpcResult()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        _output.WriteLine($"Test ID: {testId}");

        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddDebug()
            .SetMinimumLevel(LogLevel.Debug));

        services.AddRabbitX(options => options
            .UseConnection("localhost", 5672)
            .UseCredentials("rabbit", "rabbit")
            .UseVirtualHost("/")
            .WithClientName($"RabbitX.RpcResultTest.{testId}")
            .AddRpcClient<EchoRequest, EchoResponse>($"ResultClient_{testId}", rpc => rpc
                .ToExchange($"rpc.result.exchange.{testId}", "direct")
                .WithRoutingKey($"rpc.result.{testId}")
                .WithTimeout(TimeSpan.FromSeconds(10))
                .UseDirectReplyTo())
            .AddRpcHandler<EchoRequest, EchoResponse, EchoHandler>($"ResultHandler_{testId}", handler => handler
                .FromQueue($"rpc.result.queue.{testId}")
                .BindToExchange($"rpc.result.exchange.{testId}", $"rpc.result.{testId}")
                .WithPrefetchCount(10)));

        await using var serviceProvider = services.BuildServiceProvider();

        var hostedServices = serviceProvider.GetServices<IHostedService>();
        var rpcService = hostedServices.OfType<RpcConsumerHostedService>().FirstOrDefault();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await rpcService!.StartAsync(cts.Token);
        await Task.Delay(500, cts.Token);

        // Act
        var clientFactory = serviceProvider.GetRequiredService<IRpcClientFactory>();
        var client = clientFactory.CreateClient<EchoRequest, EchoResponse>($"ResultClient_{testId}");

        var request = new EchoRequest
        {
            Message = "Test RpcResult",
            Timestamp = DateTimeOffset.UtcNow
        };

        _output.WriteLine($"Sending RPC request using TryCallAsync: {request.Message}");
        var result = await client.TryCallAsync(request, cts.Token);

        // Assert
        result.Success.Should().BeTrue();
        result.Response.Should().NotBeNull();
        result.Response!.EchoedMessage.Should().Be($"Echo: {request.Message}");
        result.Duration.Should().BeLessThan(TimeSpan.FromSeconds(10));
        result.CorrelationId.Should().NotBeNullOrEmpty();

        _output.WriteLine($"RpcResult: Success={result.Success}, Duration={result.Duration.TotalMilliseconds}ms");

        // Cleanup
        await rpcService.StopAsync(cts.Token);
        await client.DisposeAsync();
    }
}

// Test request/response types
public class EchoRequest
{
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public class EchoResponse
{
    public string EchoedMessage { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

// Test RPC handler
public class EchoHandler : IRpcHandler<EchoRequest, EchoResponse>
{
    public Task<EchoResponse> HandleAsync(RpcContext<EchoRequest> context, CancellationToken cancellationToken = default)
    {
        var response = new EchoResponse
        {
            EchoedMessage = $"Echo: {context.Message.Message}",
            ProcessedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(response);
    }
}
