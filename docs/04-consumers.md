# Consumers

Consumers receive and process messages from RabbitMQ queues. RabbitX provides a clean abstraction with `IMessageHandler<T>` for processing messages and automatic lifecycle management via `IHostedService`.

## Consumer Architecture

```
              ┌──────────────────────────────┐
              │         RabbitMQ             │
              │  ┌────────────────────────┐  │
              │  │   orders.queue         │──┼────┐
              │  │   notifications.queue  │──┼────┼──┐
              │  │   events.queue         │──┼────┼──┼──┐
              │  └────────────────────────┘  │    │  │  │
              └──────────────────────────────┘    │  │  │
                                                  │  │  │
┌─────────────────────────────────────────────────┼──┼──┼─────────────────────┐
│                            Your Application     │  │  │                     │
│                                                 ▼  ▼  ▼                     │
│   ┌─────────────────────────────────────────────────────────────┐           │
│   │                ConsumerHostedService                        │           │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │           │
│   │  │ OrderConsumer│  │NotifyConsumer│  │EventConsumer │       │           │
│   │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │           │
│   └─────────┼─────────────────┼─────────────────┼───────────────┘           │
│             │                 │                 │                           │
│             ▼                 ▼                 ▼                           │
│   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                      │
│   │ OrderHandler │  │NotifyHandler │  │EventHandler  │                      │
│   └──────────────┘  └──────────────┘  └──────────────┘                      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Creating a Message Handler

### Basic Handler

```csharp
using RabbitX.Interfaces;
using RabbitX.Models;

public class OrderCreatedHandler : IMessageHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    private readonly IOrderRepository _orderRepository;

    public OrderCreatedHandler(
        ILogger<OrderCreatedHandler> logger,
        IOrderRepository orderRepository)
    {
        _logger = logger;
        _orderRepository = orderRepository;
    }

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<OrderCreatedEvent> context,
        CancellationToken cancellationToken = default)
    {
        var order = context.Message;

        _logger.LogInformation(
            "Processing order {OrderId} for customer {CustomerId}",
            order.OrderId,
            order.CustomerId);

        try
        {
            // Process the order
            await _orderRepository.ProcessAsync(order, cancellationToken);

            // Acknowledge successful processing
            return ConsumeResult.Ack;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderId}", order.OrderId);

            // Reject and don't requeue (send to DLQ if configured)
            return ConsumeResult.Reject;
        }
    }
}
```

### Configuration

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddConsumer("OrderConsumer", con => con
        .FromQueue("shop.orders.created.queue")
        .BindToExchange("shop.orders.exchange", "orders.created")
        .Durable()
        .WithPrefetchCount(10)));

// Register the message handler
services.AddMessageHandler<OrderCreatedEvent, OrderCreatedHandler>();

// Register the consumer as a hosted service
services.AddHostedConsumer<OrderCreatedEvent>("OrderConsumer");
```

## ConsumeResult

The `ConsumeResult` enum tells RabbitMQ what to do with the message after processing:

| Result | Description | Message Fate |
|--------|-------------|--------------|
| `Ack` | Successfully processed | Removed from queue |
| `Nack` | Failed, requeue | Returns to queue for retry |
| `Reject` | Failed, don't requeue | Sent to DLQ or discarded |
| `Retry` | Retry with delay | Requeued with delay (if configured) |

### Usage Examples

```csharp
public async Task<ConsumeResult> HandleAsync(
    MessageContext<OrderEvent> context,
    CancellationToken ct)
{
    try
    {
        await ProcessOrderAsync(context.Message, ct);
        return ConsumeResult.Ack;  // Success
    }
    catch (TransientException ex)
    {
        _logger.LogWarning(ex, "Transient error, will retry");
        return ConsumeResult.Nack;  // Retry immediately
    }
    catch (ValidationException ex)
    {
        _logger.LogError(ex, "Invalid message, rejecting");
        return ConsumeResult.Reject;  // Don't retry, send to DLQ
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error");
        return ConsumeResult.Retry;  // Retry with backoff
    }
}
```

## MessageContext

The `MessageContext<T>` provides access to the message and its metadata:

```csharp
public async Task<ConsumeResult> HandleAsync(
    MessageContext<OrderEvent> context,
    CancellationToken ct)
{
    // The deserialized message
    var message = context.Message;

    // Message metadata
    var messageId = context.MessageId;           // Unique message ID
    var correlationId = context.CorrelationId;   // For request tracking
    var timestamp = context.Timestamp;           // When message was published
    var redelivered = context.Redelivered;       // Is this a redelivery?

    // Custom headers
    if (context.Headers.TryGetValue("x-retry-count", out var retryCount))
    {
        _logger.LogInformation("Retry attempt: {RetryCount}", retryCount);
    }

    // Delivery info
    var exchange = context.Exchange;
    var routingKey = context.RoutingKey;
    var deliveryTag = context.DeliveryTag;

    return ConsumeResult.Ack;
}
```

### MessageContext Properties

| Property | Type | Description |
|----------|------|-------------|
| `Message` | `T` | The deserialized message |
| `MessageId` | `string` | Unique message identifier |
| `CorrelationId` | `string?` | Correlation ID for tracking |
| `Timestamp` | `DateTimeOffset` | When the message was published |
| `Redelivered` | `bool` | Whether this is a redelivery |
| `Headers` | `IReadOnlyDictionary` | Custom message headers |
| `Exchange` | `string` | Source exchange name |
| `RoutingKey` | `string` | Routing key used |
| `DeliveryTag` | `ulong` | RabbitMQ delivery tag |
| `ConsumerTag` | `string` | Consumer identifier |

## Consumer Options

### Prefetch Count (QoS)

Controls how many unacknowledged messages a consumer can have at once:

```csharp
.AddConsumer<OrderEvent, OrderHandler>("OrderConsumer", con => con
    .FromQueue("orders.queue")
    .WithPrefetchCount(10))  // Process up to 10 messages concurrently
```

**Guidelines:**
- **Low (1-5)**: For slow, resource-intensive processing
- **Medium (10-20)**: For typical business logic
- **High (50-100)**: For fast, I/O-bound processing

### Queue Declaration

```csharp
.AddConsumer("OrderConsumer", con => con
    .FromQueue("orders.queue")
    .Durable()                // Survive broker restart
    .Exclusive(false)         // Allow multiple consumers
    .AutoDelete(false))       // Don't delete when unused
```

The queue is automatically declared on consumer startup if it doesn't exist.

### Exchange Binding

```csharp
// Direct binding
.AddConsumer("OrderConsumer", con => con
    .FromQueue("orders.queue")
    .BindToExchange("orders.exchange", "orders.created"))

// Topic binding with wildcard
.AddConsumer("LogConsumer", con => con
    .FromQueue("logs.all.queue")
    .BindToExchange("logs.exchange", "logs.#", "topic"))

// For multiple bindings, use topic exchange with wildcard pattern
.AddConsumer("NotifyConsumer", con => con
    .FromQueue("notifications.queue")
    .BindToExchange("notifications.exchange", "notify.*", "topic"))  // Matches notify.email, notify.sms, notify.push
```

Note: Each consumer supports one exchange binding. Use topic exchange patterns (`*` and `#` wildcards) for multiple routing keys.

## Dead Letter Queue Configuration

Configure where failed messages go:

```csharp
.AddConsumer("OrderConsumer", con => con
    .FromQueue("orders.queue")
    .BindToExchange("orders.exchange", "orders.created")
    .WithDeadLetter(dlx => dlx
        .Exchange("orders.dlx.exchange")
        .RoutingKey("orders.failed")
        .Queue("orders.dead-letters.queue")))
```

See [Dead Letter Queues](07-dead-letter-queues.md) for detailed configuration.

## Accessing Consumer Configuration from Handlers

To access consumer configuration (retry settings, delays, etc.) from within a handler, inject `RabbitXOptions` directly. **Do NOT use `IOptions<ConsumerOptions>`** - individual consumer options are not registered separately in the DI container.

### Correct Approach

```csharp
using RabbitX.Configuration;
using RabbitX.Interfaces;
using RabbitX.Models;

public class NotificationHandler : IMessageHandler<NotificationEvent>
{
    private readonly ILogger<NotificationHandler> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan[] _retryDelays;

    public NotificationHandler(
        ILogger<NotificationHandler> logger,
        RabbitXOptions rabbitXOptions)  // Inject RabbitXOptions, NOT IOptions<ConsumerOptions>
    {
        _logger = logger;

        // Access the specific consumer configuration by name
        if (rabbitXOptions.Consumers.TryGetValue("NotificationConsumer", out var consumerOptions))
        {
            _maxRetries = consumerOptions.Retry.MaxRetries;
            _retryDelays = (consumerOptions.Retry.DelaysInSeconds ?? Array.Empty<int>())
                .Select(seconds => TimeSpan.FromSeconds(seconds))
                .ToArray();
        }
        else
        {
            // Fallback defaults if consumer not found
            _maxRetries = 3;
            _retryDelays = Array.Empty<TimeSpan>();
        }
    }

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<NotificationEvent> context,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Processing notification with MaxRetries={MaxRetries}, Delays={Delays}",
            _maxRetries,
            string.Join(", ", _retryDelays.Select(d => d.TotalSeconds + "s")));

        // Use the configuration values...
        return ConsumeResult.Ack;
    }
}
```

### Why Not IOptions<ConsumerOptions>?

RabbitX registers `RabbitXOptions` as a singleton containing all configuration:

```csharp
// This is what RabbitX registers internally:
services.AddSingleton(options);  // RabbitXOptions instance

// Consumer configurations are stored in a dictionary:
public Dictionary<string, ConsumerOptions> Consumers { get; set; }
```

Individual `ConsumerOptions` are NOT registered with the DI container, so `IOptions<ConsumerOptions>` will resolve to an empty/default instance with null values.

### Configuration Structure

```
RabbitXOptions (singleton)
├── Connection: ConnectionOptions
├── Publishers: Dictionary<string, PublisherOptions>
├── Consumers: Dictionary<string, ConsumerOptions>  ← Access via key
│   ├── "NotificationConsumer": ConsumerOptions
│   │   ├── Queue, Exchange, RoutingKey...
│   │   ├── Retry: RetryOptions
│   │   │   ├── MaxRetries: 3
│   │   │   ├── DelaysInSeconds: [10, 30, 60]
│   │   │   └── ...
│   │   └── DeadLetter: DeadLetterOptions
│   └── "OrderConsumer": ConsumerOptions
│       └── ...
├── RpcClients: Dictionary<string, RpcClientOptions>
└── RpcHandlers: Dictionary<string, RpcHandlerOptions>
```

## Error Handling Patterns

### Pattern 1: Retry with Count Limit (Using Configuration)

```csharp
public class OrderHandler : IMessageHandler<OrderEvent>
{
    private readonly ILogger<OrderHandler> _logger;
    private readonly int _maxRetries;

    public OrderHandler(
        ILogger<OrderHandler> logger,
        RabbitXOptions rabbitXOptions)
    {
        _logger = logger;
        _maxRetries = rabbitXOptions.Consumers.TryGetValue("OrderConsumer", out var options)
            ? options.Retry.MaxRetries
            : 3;  // Fallback default
    }

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<OrderEvent> context,
        CancellationToken ct)
    {
        var retryCount = GetRetryCount(context);

        try
        {
            await ProcessOrderAsync(context.Message, ct);
            return ConsumeResult.Ack;
        }
        catch (Exception ex) when (retryCount < _maxRetries)
        {
            _logger.LogWarning(ex, "Retry {Count}/{Max}", retryCount + 1, _maxRetries);
            return ConsumeResult.Nack;  // Will increment x-death count
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Max retries exceeded, rejecting");
            return ConsumeResult.Reject;
        }
    }

    private int GetRetryCount(MessageContext<OrderEvent> context)
    {
        if (context.Headers.TryGetValue("x-death", out var deaths))
        {
            // Parse x-death header for retry count
            return ((List<object>)deaths).Count;
        }
        return 0;
    }
}
```

### Pattern 2: Circuit Breaker

```csharp
public class ResilientHandler : IMessageHandler<OrderEvent>
{
    private readonly ICircuitBreaker _circuitBreaker;

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<OrderEvent> context,
        CancellationToken ct)
    {
        if (_circuitBreaker.IsOpen)
        {
            _logger.LogWarning("Circuit breaker open, requeuing message");
            return ConsumeResult.Nack;
        }

        try
        {
            await ProcessWithCircuitBreakerAsync(context.Message, ct);
            _circuitBreaker.RecordSuccess();
            return ConsumeResult.Ack;
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure();
            return _circuitBreaker.IsOpen
                ? ConsumeResult.Nack    // Requeue for later
                : ConsumeResult.Reject; // Send to DLQ
        }
    }
}
```

### Pattern 3: Idempotent Processing

```csharp
public class IdempotentHandler : IMessageHandler<OrderEvent>
{
    private readonly IProcessedMessageStore _store;

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<OrderEvent> context,
        CancellationToken ct)
    {
        // Check if already processed
        if (await _store.IsProcessedAsync(context.MessageId, ct))
        {
            _logger.LogInformation("Message {Id} already processed", context.MessageId);
            return ConsumeResult.Ack;
        }

        try
        {
            await ProcessOrderAsync(context.Message, ct);

            // Mark as processed
            await _store.MarkProcessedAsync(context.MessageId, ct);

            return ConsumeResult.Ack;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed");
            return ConsumeResult.Reject;
        }
    }
}
```

## Multiple Consumers

You can configure multiple consumers for different message types:

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")

    // Order processing consumer
    .AddConsumer("OrderCreatedConsumer", con => con
        .FromQueue("inventory.orders.created.queue")
        .BindToExchange("shop.orders.exchange", "orders.created")
        .WithPrefetchCount(10))

    // Order cancellation consumer
    .AddConsumer("OrderCancelledConsumer", con => con
        .FromQueue("inventory.orders.cancelled.queue")
        .BindToExchange("shop.orders.exchange", "orders.cancelled")
        .WithPrefetchCount(5))

    // Notification consumer
    .AddConsumer("NotificationConsumer", con => con
        .FromQueue("notifications.queue")
        .BindToExchange("notifications.exchange", "notify.#", "topic")
        .WithPrefetchCount(20)));

// Register handlers
services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
services.AddMessageHandler<OrderCancelled, OrderCancelledHandler>();
services.AddMessageHandler<NotificationEvent, NotificationHandler>();

// Register as hosted services
services.AddHostedConsumer<OrderCreated>("OrderCreatedConsumer");
services.AddHostedConsumer<OrderCancelled>("OrderCancelledConsumer");
services.AddHostedConsumer<NotificationEvent>("NotificationConsumer");
```

## Competing Consumers

For high throughput, deploy multiple instances consuming from the same queue:

```
┌─────────────────┐
│   orders.queue  │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌───────┐ ┌───────┐
│Worker1│ │Worker2│  ← Same consumer, different instances
└───────┘ └───────┘
```

Each message is delivered to only one consumer, enabling horizontal scaling.

## Complete Example

```csharp
// Messages
public record InventoryReserved(Guid OrderId, List<ReservedItem> Items);
public record ReservedItem(string Sku, int Quantity);

// Handler
public class InventoryReservedHandler : IMessageHandler<InventoryReserved>
{
    private readonly ILogger<InventoryReservedHandler> _logger;
    private readonly IShippingService _shippingService;
    private readonly IOrderRepository _orderRepository;

    public InventoryReservedHandler(
        ILogger<InventoryReservedHandler> logger,
        IShippingService shippingService,
        IOrderRepository orderRepository)
    {
        _logger = logger;
        _shippingService = shippingService;
        _orderRepository = orderRepository;
    }

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<InventoryReserved> context,
        CancellationToken ct)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Processing inventory reservation for order {OrderId}, CorrelationId: {CorrelationId}",
            message.OrderId,
            context.CorrelationId);

        try
        {
            // Verify order exists
            var order = await _orderRepository.GetByIdAsync(message.OrderId, ct);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found, rejecting", message.OrderId);
                return ConsumeResult.Reject;
            }

            // Create shipping request
            await _shippingService.CreateShipmentAsync(order, message.Items, ct);

            // Update order status
            await _orderRepository.UpdateStatusAsync(
                message.OrderId,
                OrderStatus.ReadyToShip,
                ct);

            _logger.LogInformation("Order {OrderId} ready to ship", message.OrderId);

            return ConsumeResult.Ack;
        }
        catch (ShippingServiceException ex) when (ex.IsTransient)
        {
            _logger.LogWarning(ex, "Transient shipping error, will retry");
            return ConsumeResult.Nack;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process inventory reservation");
            return ConsumeResult.Reject;
        }
    }
}

// Configuration
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddConsumer("InventoryConsumer", con => con
        .FromQueue("shipping.inventory-reserved.queue")
        .BindToExchange("warehouse.inventory.exchange", "inventory.reserved")
        .WithPrefetchCount(10)
        .WithDeadLetter(dlx => dlx
            .Exchange("shipping.dlx.exchange")
            .Queue("shipping.dead-letters.queue"))));

// Register handler
services.AddMessageHandler<InventoryReserved, InventoryReservedHandler>();

// Register as hosted service
services.AddHostedConsumer<InventoryReserved>("InventoryConsumer");
```

## See Also

- [Publishers](03-publishers.md) - Publishing messages
- [Dead Letter Queues](07-dead-letter-queues.md) - Handling failed messages
- [Retry & Resilience](06-retry-resilience.md) - Retry configuration
- [RabbitMQ Consumer Documentation](https://www.rabbitmq.com/docs/consumers) - Official docs
