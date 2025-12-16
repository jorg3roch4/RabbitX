# Publishers

Publishers are responsible for sending messages to RabbitMQ exchanges. RabbitX provides two publisher interfaces: `IMessagePublisher` for basic publishing and `IReliableMessagePublisher` for publishing with confirms.

## Publisher Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Your Application                                  │
│                                                                              │
│   ┌─────────────────┐                                                       │
│   │ IPublisherFactory│──────┐                                               │
│   └─────────────────┘      │                                                │
│                            ▼                                                │
│   ┌─────────────────────────────────────────────────────────────┐          │
│   │                    Publishers Registry                       │          │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │          │
│   │  │ OrderPub     │  │ NotifyPub    │  │ EventPub     │       │          │
│   │  └──────────────┘  └──────────────┘  └──────────────┘       │          │
│   └─────────────────────────────────────────────────────────────┘          │
│                            │                                                │
└────────────────────────────┼────────────────────────────────────────────────┘
                             │
                             ▼
              ┌──────────────────────────────┐
              │         RabbitMQ             │
              │  ┌────────────────────────┐  │
              │  │   orders.exchange      │  │
              │  │   notifications.ex     │  │
              │  │   events.exchange      │  │
              │  └────────────────────────┘  │
              └──────────────────────────────┘
```

## Basic Publishing

### Configuration

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddPublisher("OrderPublisher", pub => pub
        .ToExchange("shop.orders.exchange", "topic")
        .WithRoutingKey("orders.created")));
```

### Using the Publisher

```csharp
public class OrderService
{
    private readonly IPublisherFactory _publisherFactory;

    public OrderService(IPublisherFactory publisherFactory)
    {
        _publisherFactory = publisherFactory;
    }

    public async Task CreateOrderAsync(Order order)
    {
        // Create the configured publisher
        var publisher = _publisherFactory.CreatePublisher<OrderCreatedEvent>("OrderPublisher");

        // Create the event
        var @event = new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Total = order.Total,
            CreatedAt = DateTime.UtcNow
        };

        // Publish the message
        var result = await publisher.PublishAsync(@event);

        if (result.Success)
        {
            Console.WriteLine($"Order event published: {order.Id}");
        }
        else
        {
            Console.WriteLine($"Failed to publish: {result.ErrorMessage}");
        }
    }
}
```

## Publisher Confirms

Publisher confirms ensure that messages are safely persisted by RabbitMQ before your application continues. This is essential for reliable messaging.

**RabbitX always uses publisher confirms internally** - all publish operations wait for broker confirmation before returning success.

### How Publisher Confirms Work

```
┌──────────┐         ┌──────────────┐         ┌─────────────┐
│Publisher │         │   RabbitMQ   │         │    Queue    │
└────┬─────┘         └──────┬───────┘         └──────┬──────┘
     │                      │                        │
     │  1. Publish Message  │                        │
     │─────────────────────▶│                        │
     │                      │                        │
     │                      │  2. Persist to Queue   │
     │                      │───────────────────────▶│
     │                      │                        │
     │  3. Confirm (Ack)    │                        │
     │◀─────────────────────│                        │
     │                      │                        │
     │  ✓ Safe to continue  │                        │
     │                      │                        │
```

### Configuration

Publisher confirms are automatically enabled. You can configure the timeout for confirms:

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddPublisher("OrderPublisher", pub => pub
        .ToExchange("shop.orders.exchange", "topic")
        .WithRoutingKey("orders.created")
        .WithConfirmTimeout(TimeSpan.FromSeconds(10))));  // Confirm timeout
```

### Using the Publisher

```csharp
public class ReliableOrderService
{
    private readonly IPublisherFactory _publisherFactory;
    private readonly ILogger<ReliableOrderService> _logger;

    public ReliableOrderService(
        IPublisherFactory publisherFactory,
        ILogger<ReliableOrderService> logger)
    {
        _publisherFactory = publisherFactory;
        _logger = logger;
    }

    public async Task<bool> CreateOrderAsync(Order order)
    {
        var publisher = _publisherFactory.CreatePublisher<OrderCreatedEvent>("OrderPublisher");

        var @event = new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Total = order.Total
        };

        var result = await publisher.PublishAsync(@event);

        switch (result.Status)
        {
            case PublishStatus.Success:
                _logger.LogInformation("Order {OrderId} published and confirmed", order.Id);
                return true;

            case PublishStatus.ConfirmTimeout:
                _logger.LogWarning("Order {OrderId} published but confirm timed out", order.Id);
                return false;

            case PublishStatus.Failed:
                _logger.LogError("Order {OrderId} failed to publish: {Error}",
                    order.Id, result.ErrorMessage);
                return false;

            default:
                return false;
        }
    }
}
```

## Publish Options

You can customize individual publish operations using `PublishOptions`:

```csharp
var publisher = _publisherFactory.CreatePublisher<OrderCreatedEvent>("OrderPublisher");

var options = new PublishOptions
{
    // Override routing key for this message
    RoutingKey = "orders.priority.created",

    // Message will expire after 60 seconds if not consumed
    Expiration = TimeSpan.FromSeconds(60),

    // Higher priority (0-9, default 0)
    Priority = 5,

    // Persist message to disk
    Persistent = true,

    // Custom headers
    Headers = new Dictionary<string, object>
    {
        ["x-correlation-id"] = Guid.NewGuid().ToString(),
        ["x-source-service"] = "OrderService",
        ["x-retry-count"] = 0
    }
};

await publisher.PublishAsync(@event, options);
```

### PublishOptions Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RoutingKey` | string | Configured default | Override routing key |
| `Persistent` | bool | `true` | Persist message to disk |
| `Priority` | byte | `0` | Message priority (0-9) |
| `Expiration` | TimeSpan? | `null` | Message TTL |
| `Headers` | Dictionary | `null` | Custom message headers |
| `CorrelationId` | string | `null` | Correlation ID for tracking |
| `MessageId` | string | Auto-generated | Unique message identifier |

## PublishResult

Every publish operation returns a `PublishResult`:

```csharp
public class PublishResult
{
    public bool Success { get; }
    public PublishStatus Status { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }
    public string? MessageId { get; }
}

public enum PublishStatus
{
    Success,           // Message published (and confirmed if enabled)
    Failed,            // Publish failed
    ConfirmTimeout,    // Published but confirm timed out
    ChannelClosed      // Channel was closed unexpectedly
}
```

### Handling Results

```csharp
var result = await publisher.PublishAsync(message);

if (result.Success)
{
    _logger.LogInformation("Published message {MessageId}", result.MessageId);
}
else if (result.Status == PublishStatus.ConfirmTimeout)
{
    // Message might have been delivered - log for investigation
    _logger.LogWarning("Confirm timeout for {MessageId}", result.MessageId);
}
else
{
    // Definite failure - handle accordingly
    _logger.LogError(result.Exception, "Publish failed: {Error}", result.ErrorMessage);
    throw new PublishException(result.ErrorMessage, result.Exception);
}
```

## Exchange Types

RabbitX supports all RabbitMQ exchange types:

### Direct Exchange

Routes messages to queues based on exact routing key match.

```csharp
.AddPublisher("OrderPub", pub => pub
    .ToExchange("orders.direct.exchange", "direct")
    .WithRoutingKey("orders.created"))
```

### Topic Exchange

Routes messages based on routing key patterns with wildcards:
- `*` matches exactly one word
- `#` matches zero or more words

```csharp
.AddPublisher("LogPub", pub => pub
    .ToExchange("logs.topic.exchange", "topic")
    .WithRoutingKey("logs.error.api"))  // Matches: logs.*.api, logs.#, etc.
```

### Fanout Exchange

Broadcasts messages to all bound queues (routing key ignored).

```csharp
.AddPublisher("BroadcastPub", pub => pub
    .ToExchange("broadcast.fanout.exchange", "fanout"))
```

### Headers Exchange

Routes based on message headers instead of routing key.

```csharp
.AddPublisher("NotifyPub", pub => pub
    .ToExchange("notifications.headers.exchange", "headers"))

// When publishing, include matching headers:
await publisher.PublishAsync(message, new PublishOptions
{
    Headers = new Dictionary<string, object>
    {
        ["x-notify-type"] = "email",
        ["x-priority"] = "high"
    }
});
```

## Confirm Timeout Configuration

Configure the timeout for publisher confirms:

```csharp
.AddPublisher("OrderPub", pub => pub
    .ToExchange("orders.exchange", "topic")
    .WithRoutingKey("orders.created")
    .WithConfirmTimeout(TimeSpan.FromSeconds(10)))  // Wait up to 10s for confirm
```

If the broker doesn't confirm within the timeout, `PublishResult.Status` will be `ConfirmTimeout`.

## Best Practices

### 1. Check Publish Results for Critical Messages

```csharp
// For important messages, always check the result
var publisher = _factory.CreatePublisher<PaymentProcessedEvent>("PaymentPub");
var result = await publisher.PublishAsync(paymentEvent);

if (!result.Success)
{
    _logger.LogError("Payment event publish failed: {Error}", result.ErrorMessage);
    // Handle failure - e.g., save to outbox for retry
}
```

### 2. Set Appropriate Message TTL

```csharp
// Prevent queue buildup for time-sensitive messages
await publisher.PublishAsync(notification, new PublishOptions
{
    Expiration = TimeSpan.FromMinutes(5)
});
```

### 3. Include Correlation IDs

```csharp
var correlationId = Guid.NewGuid().ToString();

await publisher.PublishAsync(message, new PublishOptions
{
    CorrelationId = correlationId,
    Headers = new Dictionary<string, object>
    {
        ["x-trace-id"] = Activity.Current?.TraceId.ToString() ?? correlationId
    }
});
```

### 4. Handle Failures Gracefully

```csharp
public async Task PublishWithFallback<T>(T message) where T : class
{
    var publisher = _publisherFactory.CreatePublisher<T>("MyPublisher");
    var result = await publisher.PublishAsync(message);

    if (!result.Success)
    {
        // Store in outbox for retry
        await _outboxRepository.SaveAsync(message);
        _logger.LogWarning("Message saved to outbox: {Error}", result.ErrorMessage);
    }
}
```

## Complete Example

```csharp
// Messages
public record ProductCreated(Guid ProductId, string Name, decimal Price);
public record ProductUpdated(Guid ProductId, string Name, decimal Price);

// Service
public class ProductService
{
    private readonly IPublisherFactory _publisherFactory;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IPublisherFactory publisherFactory, ILogger<ProductService> logger)
    {
        _publisherFactory = publisherFactory;
        _logger = logger;
    }

    public async Task<Product> CreateProductAsync(CreateProductRequest request)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price
        };

        // Save to database
        await _repository.SaveAsync(product);

        // Publish event
        var publisher = _publisherFactory.CreatePublisher<ProductCreated>("ProductCreatedPublisher");

        var result = await publisher.PublishAsync(
            new ProductCreated(product.Id, product.Name, product.Price),
            new PublishOptions
            {
                CorrelationId = product.Id.ToString(),
                Headers = new Dictionary<string, object>
                {
                    ["x-event-type"] = "ProductCreated",
                    ["x-timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            });

        if (!result.Success)
        {
            _logger.LogError("Failed to publish ProductCreated event: {Error}", result.ErrorMessage);
        }

        return product;
    }
}

// Configuration
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddPublisher("ProductCreatedPublisher", pub => pub
        .ToExchange("catalog.products.exchange", "topic")
        .WithRoutingKey("products.created")
        .WithConfirmTimeout(TimeSpan.FromSeconds(10)))
    .AddPublisher("ProductUpdatedPublisher", pub => pub
        .ToExchange("catalog.products.exchange", "topic")
        .WithRoutingKey("products.updated")
        .WithConfirmTimeout(TimeSpan.FromSeconds(10))));
```

## See Also

- [Configuration](02-configuration.md) - All configuration options
- [Consumers](04-consumers.md) - Consuming messages
- [Retry & Resilience](06-retry-resilience.md) - Retry policies
- [RabbitMQ Publisher Confirms](https://www.rabbitmq.com/docs/confirms) - Official documentation
