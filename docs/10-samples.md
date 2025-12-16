# Sample Projects

RabbitX includes several sample projects demonstrating different messaging patterns. These samples are found in the `samples/` directory.

## Available Samples

| Sample | Description |
|--------|-------------|
| `RabbitX.Sample.Publisher` | Demonstrates publishing messages with various configurations |
| `RabbitX.Sample.Consumer` | Demonstrates consuming messages with different handlers |
| `RabbitX.Sample.Rpc` | Demonstrates the Request-Reply (RPC) pattern |
| `RabbitX.Samples.Common` | Shared code: messages, handlers, and demo utilities |

## Prerequisites

Before running the samples, ensure:

1. **.NET 10 SDK** is installed
2. **RabbitMQ** is running on `localhost:5672`
3. **Credentials** are `rabbit`/`rabbit` (or update in code)

### Quick Start with Docker

```bash
docker run -d --hostname rabbitmq \
  --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=rabbit \
  -e RABBITMQ_DEFAULT_PASS=rabbit \
  rabbitmq:3-management
```

## Sample 1: Publisher

Demonstrates publishing messages to RabbitMQ.

### Location

```
samples/RabbitX.Sample.Publisher/
```

### Running

```bash
cd samples/RabbitX.Sample.Publisher
dotnet run
```

### Features Demonstrated

- Publisher configuration via appsettings.json
- Publishing to topic exchanges
- Publisher confirms
- Message headers and metadata
- Interactive message count selection

### Key Code

```csharp
// Register RabbitX with publisher
builder.Services.AddRabbitXPublisher(builder.Configuration, config);

// Publish messages
var publisher = _publisherFactory.GetPublisher<OrderCreatedEvent>("OrderPublisher");

var result = await publisher.PublishAsync(new OrderCreatedEvent
{
    OrderId = Guid.NewGuid(),
    CustomerId = "CUST-001",
    Total = 99.99m,
    CreatedAt = DateTime.UtcNow
});
```

### Configuration (appsettings.json)

```json
{
  "RabbitX": {
    "Connection": {
      "HostName": "localhost",
      "Port": 5672,
      "UserName": "rabbit",
      "Password": "rabbit"
    },
    "Publishers": {
      "OrderPublisher": {
        "Exchange": "shop.orders.exchange",
        "ExchangeType": "topic",
        "RoutingKey": "orders.created",
        "EnablePublisherConfirms": true
      }
    }
  }
}
```

## Sample 2: Consumer

Demonstrates consuming and processing messages from RabbitMQ.

### Location

```
samples/RabbitX.Sample.Consumer/
```

### Running

```bash
cd samples/RabbitX.Sample.Consumer
dotnet run
```

For command-line options:

```bash
dotnet run -- --help
```

### Features Demonstrated

- Consumer configuration via appsettings.json
- Message handlers with `IMessageHandler<T>`
- ConsumeResult handling (Ack, Nack, Reject)
- Dead letter queue configuration
- Prefetch count tuning
- Background processing with `IHostedService`

### Key Code

```csharp
// Message handler
public class OrderCreatedHandler : IMessageHandler<OrderCreatedEvent>
{
    public async Task<ConsumeResult> HandleAsync(
        MessageContext<OrderCreatedEvent> context,
        CancellationToken cancellationToken)
    {
        var order = context.Message;

        _logger.LogInformation(
            "Processing order {OrderId} for {CustomerId}",
            order.OrderId,
            order.CustomerId);

        try
        {
            await ProcessOrderAsync(order, cancellationToken);
            return ConsumeResult.Ack;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order");
            return ConsumeResult.Reject;
        }
    }
}
```

### Configuration (appsettings.json)

```json
{
  "RabbitX": {
    "Connection": {
      "HostName": "localhost",
      "Port": 5672,
      "UserName": "rabbit",
      "Password": "rabbit"
    },
    "Consumers": {
      "OrderConsumer": {
        "Queue": "shop.orders.created.queue",
        "Exchange": "shop.orders.exchange",
        "RoutingKey": "orders.created",
        "PrefetchCount": 10,
        "DeclareQueue": true,
        "DeadLetter": {
          "Exchange": "shop.dlx.exchange",
          "Queue": "shop.orders.dead-letters.queue"
        }
      }
    }
  }
}
```

## Sample 3: RPC (Request-Reply)

Demonstrates the synchronous RPC pattern over RabbitMQ.

### Location

```
samples/RabbitX.Sample.Rpc/
```

### Running

Run both client and server together:

```bash
cd samples/RabbitX.Sample.Rpc
dotnet run
```

Run in separate terminals:

```bash
# Terminal 1: Start server
dotnet run -- server

# Terminal 2: Run client
dotnet run -- client
```

Show help:

```bash
dotnet run -- help
```

### Features Demonstrated

- RPC client configuration with `IRpcClient<TRequest, TResponse>`
- RPC handlers with `IRpcHandler<TRequest, TResponse>`
- Direct Reply-To for efficient responses
- Timeout handling
- Parallel RPC requests
- Error handling in RPC

### Demo Scenarios

The RPC sample demonstrates several scenarios:

1. **Calculator RPC** - Mathematical operations (add, subtract, multiply, divide, power)
2. **User Query RPC** - Looking up users by ID
3. **Parallel Requests** - 10 concurrent RPC calls
4. **Timeout Demo** - Handling RPC timeouts

### Key Code

```csharp
// Configuration
builder.Services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("rabbit", "rabbit")

    // RPC Client
    .AddRpcClient<CalculateRequest, CalculateResponse>("Calculator", rpc => rpc
        .ToExchange("sample.rpc.exchange", "direct")
        .WithRoutingKey("sample.calculator.compute")
        .WithTimeout(TimeSpan.FromSeconds(30))
        .UseDirectReplyTo())

    // RPC Handler
    .AddRpcHandler<CalculateRequest, CalculateResponse, CalculatorHandler>(
        "CalculatorHandler",
        handler => handler
            .FromQueue("sample.calculator.compute.queue")
            .BindToExchange("sample.rpc.exchange", "sample.calculator.compute")
            .WithPrefetchCount(10)));
```

```csharp
// Using the RPC client
var clientFactory = host.Services.GetRequiredService<IRpcClientFactory>();
await using var client = clientFactory.CreateClient<CalculateRequest, CalculateResponse>("Calculator");

// Simple call (throws on error)
var response = await client.CallAsync(new CalculateRequest
{
    A = 10,
    B = 5,
    Operation = "add"
});

Console.WriteLine($"Result: {response.Result}"); // Result: 15

// Safe call (returns RpcResult)
var result = await client.TryCallAsync(
    new CalculateRequest { A = 10, B = 0, Operation = "divide" },
    TimeSpan.FromSeconds(5));

if (result.Success)
{
    Console.WriteLine($"Result: {result.Response!.Result}");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

```csharp
// RPC Handler implementation
public class CalculatorHandler : IRpcHandler<CalculateRequest, CalculateResponse>
{
    public Task<CalculateResponse> HandleAsync(
        RpcContext<CalculateRequest> context,
        CancellationToken cancellationToken)
    {
        var request = context.Message;

        var result = request.Operation.ToLower() switch
        {
            "add" => request.A + request.B,
            "subtract" => request.A - request.B,
            "multiply" => request.A * request.B,
            "divide" when request.B != 0 => request.A / request.B,
            "divide" => throw new DivideByZeroException(),
            "power" => Math.Pow(request.A, request.B),
            _ => throw new InvalidOperationException($"Unknown operation: {request.Operation}")
        };

        return Task.FromResult(new CalculateResponse { Result = result });
    }
}
```

### Messages

```csharp
// Request
public class CalculateRequest
{
    public double A { get; set; }
    public double B { get; set; }
    public string Operation { get; set; } = string.Empty;
}

// Response
public class CalculateResponse
{
    public double Result { get; set; }
    public string? Error { get; set; }
}
```

## Sample 4: Common Library

Shared code used by the Publisher and Consumer samples.

### Location

```
samples/RabbitX.Samples.Common/
```

### Contents

| Folder | Description |
|--------|-------------|
| `Messages/` | Shared message types (OrderCreated, NotificationSent, etc.) |
| `Handlers/` | Message handlers for the consumer sample |
| `Demo/` | Demo runner utilities for interactive samples |
| `Extensions/` | Service collection extensions for sample setup |
| `Configuration/` | Configuration models and selectors |

### Message Examples

```csharp
public record OrderCreatedEvent
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<OrderLineItem> Items { get; init; } = new();
}

public record NotificationEvent
{
    public Guid NotificationId { get; init; }
    public string Type { get; init; } = string.Empty;  // email, sms, push
    public string Recipient { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}
```

## Running All Samples Together

To see the full messaging flow:

### Terminal 1: Start Consumer

```bash
cd samples/RabbitX.Sample.Consumer
dotnet run
```

### Terminal 2: Start Publisher

```bash
cd samples/RabbitX.Sample.Publisher
dotnet run
```

Enter the number of messages to publish. Watch Terminal 1 to see messages being consumed.

### Terminal 3: Run RPC Demo

```bash
cd samples/RabbitX.Sample.Rpc
dotnet run
```

## Building All Samples

```bash
cd samples
dotnet build
```

## Sample Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Sample Project Structure                            │
│                                                                              │
│  RabbitX.Sample.Publisher ──────────┐                                       │
│  (Console App)                      │                                       │
│                                     ▼                                       │
│                            ┌─────────────────┐                              │
│                            │    RabbitMQ     │                              │
│                            │  (localhost)    │                              │
│                            └────────┬────────┘                              │
│                                     │                                       │
│                                     ▼                                       │
│  RabbitX.Sample.Consumer ◄──────────┘                                       │
│  (Background Service)                                                       │
│                                                                              │
│                                                                              │
│  RabbitX.Sample.Rpc ◄───────────────────────────────────────────────────────│
│  (Console App)              Client ◄─────────────────────────▶ Handler      │
│                                         RPC Pattern                         │
│                                                                              │
│                                                                              │
│  RabbitX.Samples.Common                                                     │
│  (Class Library)                                                            │
│    ├── Messages/         - Shared message types                             │
│    ├── Handlers/         - IMessageHandler implementations                  │
│    ├── Demo/             - Interactive demo utilities                       │
│    └── Extensions/       - Service registration helpers                     │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## See Also

- [Getting Started](01-getting-started.md) - Installation and first steps
- [Publishers](03-publishers.md) - Publisher configuration details
- [Consumers](04-consumers.md) - Consumer configuration details
- [RPC](05-rpc.md) - RPC pattern documentation
