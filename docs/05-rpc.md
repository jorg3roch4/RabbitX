# RPC (Request-Reply Pattern)

RabbitX provides first-class support for the RPC (Remote Procedure Call) pattern, enabling synchronous request-reply communication over asynchronous messaging infrastructure.

## The Sync-Async-Sync Pattern

The RPC implementation follows the **Sync-Async-Sync** pattern:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                          Sync-Async-Sync Pattern                              │
│                                                                               │
│  CLIENT                           RABBITMQ                           HANDLER │
│  ──────                           ────────                           ─────── │
│                                                                               │
│  1. SYNC                                                                      │
│  ┌─────────────┐                                                              │
│  │ CallAsync() │ ─── await ───┐                                               │
│  └─────────────┘              │                                               │
│                               │                                               │
│  2. ASYNC                     ▼                                               │
│                    ┌────────────────────┐      ┌────────────────────┐        │
│                    │  Request Queue     │─────▶│  HandleAsync()     │        │
│                    └────────────────────┘      └─────────┬──────────┘        │
│                                                          │                    │
│  3. ASYNC                                                │                    │
│                    ┌────────────────────┐                │                    │
│                    │  Reply Queue       │◀───────────────┘                    │
│                    └─────────┬──────────┘                                     │
│                              │                                                │
│  4. SYNC                     ▼                                                │
│  ┌─────────────┐  ┌───────────────────┐                                      │
│  │  Response   │◀─│ TaskCompletion    │                                      │
│  └─────────────┘  │ Source resolves   │                                      │
│                   └───────────────────┘                                      │
│                                                                               │
└──────────────────────────────────────────────────────────────────────────────┘
```

**Flow:**
1. **SYNC**: Client calls `CallAsync()` and awaits
2. **ASYNC**: Message travels through RabbitMQ to handler
3. **ASYNC**: Response travels back through reply queue
4. **SYNC**: Client's await completes with response

## RPC Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              RPC Client                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      IRpcClientFactory                               │    │
│  │   CreateClient<TRequest, TResponse>("ClientName")                   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                  IRpcClient<TRequest, TResponse>                     │    │
│  │                                                                      │    │
│  │   • CallAsync(request) → TResponse                                  │    │
│  │   • TryCallAsync(request) → RpcResult<TResponse>                    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     │ CorrelationId + ReplyTo
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                               RabbitMQ                                       │
│   ┌─────────────────┐                          ┌─────────────────┐          │
│   │ Request Queue   │                          │ Reply Queue     │          │
│   │ (rpc.requests)  │                          │ (amq.reply-to)  │          │
│   └────────┬────────┘                          └────────▲────────┘          │
│            │                                            │                    │
└────────────┼────────────────────────────────────────────┼────────────────────┘
             │                                            │
             ▼                                            │
┌─────────────────────────────────────────────────────────────────────────────┐
│                              RPC Handler                                     │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                IRpcHandler<TRequest, TResponse>                      │    │
│  │                                                                      │    │
│  │   HandleAsync(RpcContext<TRequest>) → TResponse                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Quick Start

### 1. Define Request/Response Types

```csharp
// Request
public class GetProductRequest
{
    public Guid ProductId { get; set; }
}

// Response
public class GetProductResponse
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}
```

### 2. Create the RPC Handler

```csharp
using RabbitX.Rpc;

public class GetProductHandler : IRpcHandler<GetProductRequest, GetProductResponse>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<GetProductHandler> _logger;

    public GetProductHandler(
        IProductRepository repository,
        ILogger<GetProductHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<GetProductResponse> HandleAsync(
        RpcContext<GetProductRequest> context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing GetProduct request for {ProductId}, CorrelationId: {CorrelationId}",
            context.Message.ProductId,
            context.CorrelationId);

        var product = await _repository.GetByIdAsync(
            context.Message.ProductId,
            cancellationToken);

        if (product == null)
        {
            return new GetProductResponse
            {
                ProductId = context.Message.ProductId,
                Name = "Not Found",
                Price = 0,
                StockQuantity = 0
            };
        }

        return new GetProductResponse
        {
            ProductId = product.Id,
            Name = product.Name,
            Price = product.Price,
            StockQuantity = product.StockQuantity
        };
    }
}
```

### 3. Configure RPC

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .WithClientName("CatalogService")

    // RPC Client (caller side)
    .AddRpcClient<GetProductRequest, GetProductResponse>("ProductRpc", rpc => rpc
        .ToExchange("catalog.rpc.exchange", "direct")
        .WithRoutingKey("catalog.products.get")
        .WithTimeout(TimeSpan.FromSeconds(10))
        .UseDirectReplyTo())

    // RPC Handler (server side)
    .AddRpcHandler<GetProductRequest, GetProductResponse, GetProductHandler>("ProductRpcHandler", h => h
        .FromQueue("catalog.products.get.queue")
        .BindToExchange("catalog.rpc.exchange", "catalog.products.get")
        .WithPrefetchCount(20)));
```

### 4. Use the RPC Client

```csharp
public class ProductService
{
    private readonly IRpcClientFactory _rpcFactory;

    public ProductService(IRpcClientFactory rpcFactory)
    {
        _rpcFactory = rpcFactory;
    }

    public async Task<GetProductResponse> GetProductAsync(Guid productId)
    {
        await using var client = _rpcFactory.CreateClient<GetProductRequest, GetProductResponse>("ProductRpc");

        var response = await client.CallAsync(new GetProductRequest
        {
            ProductId = productId
        });

        return response;
    }
}
```

## RPC Client Methods

### CallAsync - Throws on Error

```csharp
await using var client = factory.CreateClient<TRequest, TResponse>("ClientName");

try
{
    // Throws TimeoutException or InvalidOperationException on failure
    var response = await client.CallAsync(request);
    Console.WriteLine($"Got response: {response}");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Request timed out: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"RPC failed: {ex.Message}");
}
```

### TryCallAsync - Returns RpcResult

```csharp
await using var client = factory.CreateClient<TRequest, TResponse>("ClientName");

// Never throws - check result instead
var result = await client.TryCallAsync(request);

if (result.Success)
{
    Console.WriteLine($"Response: {result.Response}");
    Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds}ms");
}
else if (result.IsTimeout)
{
    Console.WriteLine($"Timeout after {result.Duration.TotalSeconds}s");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

### TryCallAsync with Custom Timeout

```csharp
// Override default timeout for this specific call
var result = await client.TryCallAsync(request, TimeSpan.FromSeconds(5));
```

## RpcResult

The `RpcResult<TResponse>` provides detailed information about the RPC call:

```csharp
public class RpcResult<TResponse>
{
    public bool Success { get; }           // True if response received
    public TResponse? Response { get; }    // The response (if successful)
    public string? CorrelationId { get; }  // Request correlation ID
    public TimeSpan Duration { get; }      // Total call duration
    public bool IsTimeout { get; }         // True if timed out
    public string? ErrorMessage { get; }   // Error description
    public Exception? Exception { get; }   // Exception (if any)
}
```

### Result Handling Patterns

```csharp
var result = await client.TryCallAsync(request);

// Pattern 1: Simple check
if (result.Success)
{
    return result.Response;
}

// Pattern 2: Detailed handling
switch (result)
{
    case { Success: true }:
        _logger.LogInformation("RPC succeeded in {Duration}ms", result.Duration.TotalMilliseconds);
        return result.Response;

    case { IsTimeout: true }:
        _logger.LogWarning("RPC timeout after {Duration}s", result.Duration.TotalSeconds);
        throw new ServiceUnavailableException("Service did not respond in time");

    default:
        _logger.LogError(result.Exception, "RPC failed: {Error}", result.ErrorMessage);
        throw new RpcException(result.ErrorMessage, result.Exception);
}
```

## RpcContext

The `RpcContext<TRequest>` provides request metadata to handlers:

```csharp
public async Task<TResponse> HandleAsync(
    RpcContext<TRequest> context,
    CancellationToken ct)
{
    // The request message
    var request = context.Message;

    // Request metadata
    var messageId = context.MessageId;         // Unique request ID
    var correlationId = context.CorrelationId; // For tracking
    var replyTo = context.ReplyTo;             // Reply queue
    var timestamp = context.Timestamp;         // Request timestamp

    // Custom headers
    if (context.Headers?.TryGetValue("x-user-id", out var userId) == true)
    {
        _logger.LogInformation("Request from user: {UserId}", userId);
    }

    return response;
}
```

## Direct Reply-to vs Custom Reply Queue

### Direct Reply-to (Recommended)

Uses RabbitMQ's built-in `amq.rabbitmq.reply-to` pseudo-queue:

```csharp
.AddRpcClient<TReq, TRes>("Client", rpc => rpc
    .ToExchange("rpc.exchange", "direct")
    .WithRoutingKey("rpc.request")
    .UseDirectReplyTo())  // Uses amq.rabbitmq.reply-to
```

**Advantages:**
- No queue declaration needed
- Lower latency
- Automatic cleanup

### Custom Reply Queue

Uses a dedicated reply queue per client:

```csharp
.AddRpcClient<TReq, TRes>("Client", rpc => rpc
    .ToExchange("rpc.exchange", "direct")
    .WithRoutingKey("rpc.request")
    .WithReplyQueuePrefix("myapp.replies"))  // Creates myapp.replies.{clientId}
```

**Use when:**
- Need reply queue monitoring
- Debugging/troubleshooting
- Specific queue policies needed

## Timeout Configuration

### Default Timeout (Configuration)

```csharp
.AddRpcClient<TReq, TRes>("Client", rpc => rpc
    .ToExchange("rpc.exchange", "direct")
    .WithRoutingKey("rpc.request")
    .WithTimeout(TimeSpan.FromSeconds(30)))  // Default for all calls
```

### Per-Call Timeout

```csharp
// Override for specific call
var result = await client.TryCallAsync(request, TimeSpan.FromSeconds(5));
```

### Timeout Guidelines

| Scenario | Recommended Timeout |
|----------|---------------------|
| Fast lookups (cache hit) | 1-5 seconds |
| Database queries | 5-15 seconds |
| External API calls | 15-30 seconds |
| Complex processing | 30-60 seconds |

## Error Handling in Handlers

```csharp
public class OrderHandler : IRpcHandler<CreateOrderRequest, CreateOrderResponse>
{
    public async Task<CreateOrderResponse> HandleAsync(
        RpcContext<CreateOrderRequest> context,
        CancellationToken ct)
    {
        try
        {
            var order = await CreateOrderAsync(context.Message, ct);

            return new CreateOrderResponse
            {
                Success = true,
                OrderId = order.Id,
                Message = "Order created successfully"
            };
        }
        catch (ValidationException ex)
        {
            // Return error in response (don't throw)
            return new CreateOrderResponse
            {
                Success = false,
                Message = ex.Message,
                ValidationErrors = ex.Errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order");

            return new CreateOrderResponse
            {
                Success = false,
                Message = "An unexpected error occurred"
            };
        }
    }
}
```

## Parallel RPC Calls

Execute multiple RPC calls concurrently:

```csharp
public async Task<OrderSummary> GetOrderSummaryAsync(Guid orderId)
{
    await using var orderClient = _factory.CreateClient<GetOrderRequest, GetOrderResponse>("OrderRpc");
    await using var customerClient = _factory.CreateClient<GetCustomerRequest, GetCustomerResponse>("CustomerRpc");
    await using var inventoryClient = _factory.CreateClient<CheckStockRequest, CheckStockResponse>("InventoryRpc");

    // Execute all RPC calls in parallel
    var orderTask = orderClient.TryCallAsync(new GetOrderRequest { OrderId = orderId });
    var customerTask = customerClient.TryCallAsync(new GetCustomerRequest { CustomerId = customerId });
    var stockTask = inventoryClient.TryCallAsync(new CheckStockRequest { ProductIds = productIds });

    await Task.WhenAll(orderTask, customerTask, stockTask);

    var orderResult = await orderTask;
    var customerResult = await customerTask;
    var stockResult = await stockTask;

    // Combine results
    return new OrderSummary
    {
        Order = orderResult.Success ? orderResult.Response : null,
        Customer = customerResult.Success ? customerResult.Response : null,
        StockStatus = stockResult.Success ? stockResult.Response : null
    };
}
```

## Complete Example

See the [RabbitX.Sample.Rpc](/samples/RabbitX.Sample.Rpc) project for a complete working example with:
- Calculator RPC (arithmetic operations)
- User Query RPC (simulated database lookup)
- Parallel requests demonstration
- Timeout handling

```csharp
// Configuration from the sample
builder.Services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("rabbit", "rabbit")
    .WithClientName("RabbitX.Sample.Rpc")

    // Calculator RPC
    .AddRpcClient<CalculateRequest, CalculateResponse>("Calculator", rpc => rpc
        .ToExchange("sample.rpc.exchange", "direct")
        .WithRoutingKey("sample.calculator.compute")
        .WithTimeout(TimeSpan.FromSeconds(30))
        .UseDirectReplyTo())
    .AddRpcHandler<CalculateRequest, CalculateResponse, CalculatorHandler>("CalculatorHandler", h => h
        .FromQueue("sample.calculator.compute.queue")
        .BindToExchange("sample.rpc.exchange", "sample.calculator.compute")
        .WithPrefetchCount(10))

    // User Query RPC
    .AddRpcClient<GetUserRequest, GetUserResponse>("UserQuery", rpc => rpc
        .ToExchange("sample.rpc.exchange", "direct")
        .WithRoutingKey("sample.users.get-by-id")
        .WithTimeout(TimeSpan.FromSeconds(10))
        .UseDirectReplyTo())
    .AddRpcHandler<GetUserRequest, GetUserResponse, UserQueryHandler>("UserQueryHandler", h => h
        .FromQueue("sample.users.get-by-id.queue")
        .BindToExchange("sample.rpc.exchange", "sample.users.get-by-id")
        .WithPrefetchCount(10)));
```

## Best Practices

### 1. Always Dispose Clients

```csharp
// Use 'await using' for automatic disposal
await using var client = factory.CreateClient<TReq, TRes>("Name");
var response = await client.CallAsync(request);
```

### 2. Use TryCallAsync for Non-Critical Calls

```csharp
// Don't fail the entire operation if RPC fails
var result = await client.TryCallAsync(request);
return result.Success ? result.Response : defaultValue;
```

### 3. Set Appropriate Timeouts

```csharp
// Fast operations: short timeout
.WithTimeout(TimeSpan.FromSeconds(5))

// Complex operations: longer timeout
.WithTimeout(TimeSpan.FromSeconds(30))
```

### 4. Include Business Errors in Response

```csharp
// Response type with error information
public class CreateOrderResponse
{
    public bool Success { get; set; }
    public Guid? OrderId { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? ValidationErrors { get; set; }
}
```

### 5. Log CorrelationIds for Tracing

```csharp
_logger.LogInformation(
    "RPC call completed. CorrelationId: {CorrelationId}, Duration: {Duration}ms",
    result.CorrelationId,
    result.Duration.TotalMilliseconds);
```

## See Also

- [Configuration](02-configuration.md) - RPC configuration options
- [Naming Conventions](09-naming-conventions.md) - Queue naming for RPC
- [Samples](10-samples.md) - RPC sample project
- [RabbitMQ RPC Tutorial](https://www.rabbitmq.com/tutorials/tutorial-six-dotnet) - Official tutorial
