# Dead Letter Queues

Dead Letter Queues (DLQ) capture messages that cannot be processed successfully, enabling investigation and recovery of failed messages.

## What is a Dead Letter Queue?

When a message cannot be processed, instead of being lost, it's routed to a special queue called a Dead Letter Queue:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Dead Letter Flow                                    │
│                                                                              │
│  ┌──────────┐     ┌─────────────────┐     ┌──────────────┐                  │
│  │ Publisher│────▶│  Main Exchange  │────▶│  Main Queue  │                  │
│  └──────────┘     └─────────────────┘     └──────┬───────┘                  │
│                                                  │                           │
│                                                  ▼                           │
│                                           ┌────────────┐                    │
│                                           │  Consumer  │                    │
│                                           └──────┬─────┘                    │
│                                                  │                           │
│                               ┌──────────────────┼──────────────────┐       │
│                               │                  │                  │       │
│                               ▼                  ▼                  ▼       │
│                          ┌────────┐         ┌────────┐         ┌────────┐  │
│                          │  ACK   │         │  NACK  │         │ REJECT │  │
│                          │(success)│        │(requeue)│        │(no req)│  │
│                          └────────┘         └────┬───┘         └────┬───┘  │
│                                                  │                  │       │
│                                                  │ After max        │       │
│                                                  │ retries          │       │
│                                                  ▼                  ▼       │
│                                           ┌─────────────────────────────┐  │
│                                           │    Dead Letter Exchange     │  │
│                                           └──────────────┬──────────────┘  │
│                                                          │                  │
│                                                          ▼                  │
│                                           ┌─────────────────────────────┐  │
│                                           │    Dead Letter Queue        │  │
│                                           │  (for investigation/retry)  │  │
│                                           └─────────────────────────────┘  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## When Messages Are Dead-Lettered

Messages are sent to the DLQ when:

1. **Rejected without requeue** - Consumer returns `ConsumeResult.Reject`
2. **Message TTL expires** - Message sits in queue longer than its TTL
3. **Queue length exceeded** - Queue is full and oldest messages are dropped
4. **Max retries exceeded** - After NACKing too many times

## Configuration

### Basic DLQ Setup

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddConsumer("OrderConsumer", con => con
        .FromQueue("shop.orders.queue")
        .BindToExchange("shop.orders.exchange", "orders.#", "topic")
        .WithDeadLetter(dlx => dlx
            .Exchange("shop.dlx.exchange")
            .RoutingKey("orders.failed")
            .Queue("shop.orders.dead-letters.queue"))));

services.AddMessageHandler<OrderEvent, OrderHandler>();
services.AddHostedConsumer<OrderEvent>("OrderConsumer");
```

### Dead Letter Options

| Method | Description |
|--------|-------------|
| `Exchange(name)` | Dead letter exchange name |
| `RoutingKey(key)` | Routing key for dead letters |
| `Queue(name)` | Dead letter queue name |
| `WithExchangeType(type)` | Exchange type (default: direct) |
| `Durable(bool)` | DLX durability (default: true) |

## Message Flow Examples

### Example 1: Simple Rejection

```csharp
public async Task<ConsumeResult> HandleAsync(
    MessageContext<OrderEvent> context,
    CancellationToken ct)
{
    try
    {
        await ProcessOrderAsync(context.Message, ct);
        return ConsumeResult.Ack;
    }
    catch (InvalidOrderException ex)
    {
        _logger.LogError(ex, "Invalid order, sending to DLQ");
        return ConsumeResult.Reject;  // → Goes to DLQ
    }
}
```

### Example 2: Retry Then Dead-Letter

```csharp
public async Task<ConsumeResult> HandleAsync(
    MessageContext<OrderEvent> context,
    CancellationToken ct)
{
    var retryCount = GetRetryCount(context);
    const int maxRetries = 3;

    try
    {
        await ProcessOrderAsync(context.Message, ct);
        return ConsumeResult.Ack;
    }
    catch (Exception ex) when (retryCount < maxRetries)
    {
        _logger.LogWarning(ex, "Attempt {N}/{Max} failed, retrying",
            retryCount + 1, maxRetries);
        return ConsumeResult.Nack;  // Requeue for retry
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Max retries exceeded, sending to DLQ");
        return ConsumeResult.Reject;  // → Goes to DLQ
    }
}
```

## Dead Letter Headers

When a message is dead-lettered, RabbitMQ adds special headers:

| Header | Description |
|--------|-------------|
| `x-death` | Array of death information |
| `x-first-death-reason` | Why it was first dead-lettered |
| `x-first-death-queue` | Original queue name |
| `x-first-death-exchange` | Original exchange |

### Reading Death Information

```csharp
public async Task<ConsumeResult> HandleDeadLetterAsync(
    MessageContext<OrderEvent> context,
    CancellationToken ct)
{
    // Get death information
    if (context.Headers.TryGetValue("x-death", out var deaths))
    {
        var deathList = (List<object>)deaths;
        foreach (var death in deathList.Cast<Dictionary<string, object>>())
        {
            var reason = death["reason"]?.ToString();
            var queue = death["queue"]?.ToString();
            var count = death["count"];
            var time = death["time"];

            _logger.LogInformation(
                "Death info - Reason: {Reason}, Queue: {Queue}, Count: {Count}",
                reason, queue, count);
        }
    }

    if (context.Headers.TryGetValue("x-first-death-reason", out var firstReason))
    {
        _logger.LogInformation("First death reason: {Reason}", firstReason);
    }

    // Process or store for investigation
    await _deadLetterRepository.SaveAsync(context.Message);

    return ConsumeResult.Ack;
}
```

## DLQ Processing Strategies

### Strategy 1: Alert and Store

```csharp
public class DeadLetterHandler : IMessageHandler<OrderEvent>
{
    private readonly IDeadLetterRepository _repository;
    private readonly IAlertService _alerts;

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<OrderEvent> context,
        CancellationToken ct)
    {
        // Store for later investigation
        await _repository.SaveAsync(new DeadLetter
        {
            MessageId = context.MessageId,
            OriginalQueue = GetOriginalQueue(context),
            Payload = JsonSerializer.Serialize(context.Message),
            FailedAt = DateTime.UtcNow,
            Reason = GetDeathReason(context)
        });

        // Alert operations team
        await _alerts.SendAsync(new Alert
        {
            Severity = AlertSeverity.Warning,
            Title = "Message Dead-Lettered",
            Message = $"Order {context.Message.OrderId} failed processing"
        });

        return ConsumeResult.Ack;
    }
}
```

### Strategy 2: Automatic Retry with Delay

```csharp
public class DelayedRetryHandler : IMessageHandler<OrderEvent>
{
    private readonly IPublisherFactory _publisherFactory;

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<OrderEvent> context,
        CancellationToken ct)
    {
        var deathCount = GetDeathCount(context);

        if (deathCount >= 5)
        {
            _logger.LogError("Message exceeded max deaths, archiving");
            await ArchiveMessageAsync(context);
            return ConsumeResult.Ack;
        }

        // Wait before republishing (exponential backoff)
        var delay = TimeSpan.FromSeconds(Math.Pow(2, deathCount));
        await Task.Delay(delay, ct);

        // Republish to original queue
        var publisher = _publisherFactory.GetPublisher<OrderEvent>("OrderPublisher");
        await publisher.PublishAsync(context.Message);

        return ConsumeResult.Ack;
    }
}
```

### Strategy 3: Manual Review Queue

```csharp
// Configuration for manual review
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")

    // DLQ consumer that moves to review queue
    .AddConsumer("DLQConsumer", con => con
        .FromQueue("orders.dead-letters.queue"))

    // Manual review queue (processed by operators)
    .AddPublisher("ReviewPublisher", pub => pub
        .ToExchange("review.exchange", "direct")
        .WithRoutingKey("review.orders")));

services.AddMessageHandler<OrderEvent, ReviewQueueHandler>();
services.AddHostedConsumer<OrderEvent>("DLQConsumer");
```

## Complete Configuration Example

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")

    // Main order processing
    .AddConsumer("OrderConsumer", con => con
        .FromQueue("ecommerce.orders.process.queue")
        .BindToExchange("ecommerce.orders.exchange", "orders.created")
        .WithPrefetchCount(10)
        .WithDeadLetter(dlx => dlx
            .Exchange("ecommerce.dlx.exchange")
            .RoutingKey("orders.dead")
            .Queue("ecommerce.orders.dead-letters.queue")))

    // Dead letter processing
    .AddConsumer("DLQConsumer", con => con
        .FromQueue("ecommerce.orders.dead-letters.queue")
        .WithPrefetchCount(1)));

// Register handlers
services.AddMessageHandler<OrderEvent, OrderHandler>();
services.AddMessageHandler<OrderEvent, DeadLetterHandler>();

// Register as hosted services
services.AddHostedConsumer<OrderEvent>("OrderConsumer");
services.AddHostedConsumer<OrderEvent>("DLQConsumer");
```

## Monitoring Dead Letter Queues

### RabbitMQ Management UI

Monitor DLQ depth at: `http://localhost:15672/#/queues`

### Programmatic Monitoring

```csharp
public class DlqMonitorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var queueInfo = await GetQueueInfoAsync("orders.dead-letters.queue");

            if (queueInfo.MessageCount > 100)
            {
                _logger.LogWarning(
                    "DLQ has {Count} messages pending review",
                    queueInfo.MessageCount);

                await _alerts.SendAsync(new Alert
                {
                    Title = "High DLQ Depth",
                    Message = $"Dead letter queue has {queueInfo.MessageCount} messages"
                });
            }

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
```

## Best Practices

### 1. Always Configure DLQ for Important Queues

```csharp
.AddConsumer("Orders", con => con
    .FromQueue("orders.queue")
    .WithDeadLetter(dlx => dlx
        .Exchange("dlx.exchange")
        .Queue("orders.dlq")))
```

### 2. Use Descriptive DLQ Names

```csharp
// Good: Clear naming
.Queue("ecommerce.orders.dead-letters.queue")

// Avoid: Generic names
.Queue("dlq")
```

### 3. Set Up Alerting

Monitor DLQ depth and alert when messages accumulate.

### 4. Implement DLQ Consumers

Don't let dead letters accumulate forever—process or archive them.

### 5. Include Context in Messages

```csharp
// Include debugging information
public class OrderEvent
{
    public Guid OrderId { get; set; }
    public string Source { get; set; }        // Which service sent it
    public DateTime CreatedAt { get; set; }   // When it was created
    public string TraceId { get; set; }       // For distributed tracing
}
```

## See Also

- [Consumers](04-consumers.md) - ConsumeResult options
- [Retry & Resilience](06-retry-resilience.md) - Retry before dead-lettering
- [RabbitMQ Dead Lettering](https://www.rabbitmq.com/docs/dlx) - Official documentation
