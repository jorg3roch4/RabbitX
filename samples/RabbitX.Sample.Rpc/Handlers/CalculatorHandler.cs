using System.Diagnostics;
using RabbitX.Rpc;
using RabbitX.Sample.Rpc.Messages;

namespace RabbitX.Sample.Rpc.Handlers;

/// <summary>
/// RPC handler for calculator operations.
/// Demonstrates the handler side of the Sync-Async-Sync pattern.
/// </summary>
public sealed class CalculatorHandler : IRpcHandler<CalculateRequest, CalculateResponse>
{
    private readonly ILogger<CalculatorHandler> _logger;

    public CalculatorHandler(ILogger<CalculatorHandler> logger)
    {
        _logger = logger;
    }

    public async Task<CalculateResponse> HandleAsync(
        RpcContext<CalculateRequest> context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Processing calculation request: {A} {Op} {B}, CorrelationId: {CorrelationId}",
            context.Message.A,
            context.Message.Operation,
            context.Message.B,
            context.CorrelationId);

        // Simulate some processing time
        await Task.Delay(Random.Shared.Next(50, 200), cancellationToken);

        double result;
        string? error = null;

        try
        {
            result = context.Message.Operation.ToLowerInvariant() switch
            {
                "add" or "+" => context.Message.A + context.Message.B,
                "subtract" or "-" => context.Message.A - context.Message.B,
                "multiply" or "*" => context.Message.A * context.Message.B,
                "divide" or "/" => context.Message.B != 0
                    ? context.Message.A / context.Message.B
                    : throw new DivideByZeroException("Cannot divide by zero"),
                "power" or "^" => Math.Pow(context.Message.A, context.Message.B),
                _ => throw new ArgumentException($"Unknown operation: {context.Message.Operation}")
            };
        }
        catch (Exception ex)
        {
            error = ex.Message;
            result = 0;
        }

        stopwatch.Stop();

        var response = new CalculateResponse
        {
            Result = result,
            Error = error,
            ProcessingTime = stopwatch.Elapsed,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        _logger.LogInformation(
            "Calculation complete: {A} {Op} {B} = {Result}, Duration: {Duration}ms",
            context.Message.A,
            context.Message.Operation,
            context.Message.B,
            result,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}

/// <summary>
/// RPC handler for user queries.
/// Simulates a database lookup.
/// </summary>
public sealed class UserQueryHandler : IRpcHandler<GetUserRequest, GetUserResponse>
{
    private readonly ILogger<UserQueryHandler> _logger;

    // Simulated user database
    private static readonly Dictionary<int, (string Name, string Email, DateTimeOffset CreatedAt)> Users = new()
    {
        { 1, ("Alice Smith", "alice@example.com", DateTimeOffset.UtcNow.AddDays(-100)) },
        { 2, ("Bob Johnson", "bob@example.com", DateTimeOffset.UtcNow.AddDays(-50)) },
        { 3, ("Charlie Brown", "charlie@example.com", DateTimeOffset.UtcNow.AddDays(-25)) }
    };

    public UserQueryHandler(ILogger<UserQueryHandler> logger)
    {
        _logger = logger;
    }

    public async Task<GetUserResponse> HandleAsync(
        RpcContext<GetUserRequest> context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Looking up user: {UserId}, CorrelationId: {CorrelationId}",
            context.Message.UserId,
            context.CorrelationId);

        // Simulate database lookup
        await Task.Delay(Random.Shared.Next(30, 100), cancellationToken);

        if (Users.TryGetValue(context.Message.UserId, out var user))
        {
            return new GetUserResponse
            {
                UserId = context.Message.UserId,
                Name = user.Name,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            };
        }

        return new GetUserResponse
        {
            UserId = context.Message.UserId,
            Name = "Unknown",
            Email = "unknown@example.com",
            CreatedAt = DateTimeOffset.MinValue
        };
    }
}
