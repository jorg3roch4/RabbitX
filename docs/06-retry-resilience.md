# Retry & Resilience

RabbitX provides built-in retry policies for **message consumers** using Polly. When message processing fails, RabbitX can automatically retry with configurable delays, backoff strategies, and exhaustion actions.

## Overview

Transient failures are common in distributed systems. RabbitX handles these gracefully with configurable retry policies for consumers:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                   Consumer Retry Flow with Exponential Backoff                │
│                                                                               │
│  Attempt 1          Attempt 2          Attempt 3          Exhausted          │
│  ─────────          ─────────          ─────────          ─────────          │
│                                                                               │
│  ┌───────┐         ┌───────┐          ┌───────┐         ┌───────────┐       │
│  │Process│──FAIL──▶│ Wait  │──FAIL──▶ │ Wait  │──FAIL──▶│ DLQ/Reject│       │
│  └───────┘         │  10s  │          │  30s  │         └───────────┘       │
│                    └───────┘          └───────┘                              │
│                                                                               │
│  Time:  0s          10s                40s               70s                 │
│                                                                               │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Configuration

### Basic Retry Configuration

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddConsumer("OrderConsumer", con => con
        .FromQueue("orders.queue")
        .BindToExchange("orders.exchange", "orders.created")
        .WithRetry(retry => retry
            .MaxRetries(3)
            .WithDelays(
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(60))
            .WithJitter()
            .ThenSendToDeadLetter())));

services.AddMessageHandler<OrderEvent, OrderHandler>();
services.AddHostedConsumer<OrderEvent>("OrderConsumer");
```

### Retry Options

| Method | Description | Default |
|--------|-------------|---------|
| `MaxRetries(int)` | Maximum retry attempts | `3` |
| `NoRetries()` | Disable retries | - |
| `WithDelays(TimeSpan...)` | Specific delay for each retry | - |
| `WithDelaysInSeconds(int...)` | Specific delays in seconds | - |
| `ExponentialBackoff(initial, max, multiplier)` | Use exponential backoff | - |
| `LinearBackoff(delay)` | Use constant delay | - |
| `WithJitter(bool)` | Add random variation to delays | `true` |
| `NoJitter()` | Disable jitter | - |
| `OnExhausted(action)` | Action when retries exhausted | `SendToDeadLetter` |
| `ThenSendToDeadLetter()` | Send to DLQ when exhausted | - |
| `ThenRequeue()` | Requeue when exhausted | - |
| `ThenDiscard()` | Discard when exhausted | - |

## Delay Strategies

### Specific Delays

Define exact delays for each retry attempt:

```csharp
.WithRetry(retry => retry
    .WithDelays(
        TimeSpan.FromSeconds(10),   // 1st retry after 10s
        TimeSpan.FromSeconds(30),   // 2nd retry after 30s
        TimeSpan.FromSeconds(60),   // 3rd retry after 60s
        TimeSpan.FromSeconds(120),  // 4th retry after 120s
        TimeSpan.FromSeconds(300))  // 5th retry after 300s
    .WithJitter()
    .ThenSendToDeadLetter())
```

**Note:** When using `WithDelays()`, `MaxRetries` is automatically set to the number of delays provided.

### Exponential Backoff (Recommended)

Delays increase exponentially, reducing pressure on failing downstream systems:

```csharp
.WithRetry(retry => retry
    .MaxRetries(5)
    .ExponentialBackoff(
        initialDelay: TimeSpan.FromSeconds(5),
        maxDelay: TimeSpan.FromMinutes(5),
        multiplier: 2.0)  // 5s, 10s, 20s, 40s, 80s (capped at 5min)
    .WithJitter()
    .ThenSendToDeadLetter())
```

**Delay progression with multiplier 2.0:**

```
Attempt │ Base Delay │ With Jitter (example)
────────┼────────────┼──────────────────────
   1    │     5s     │    4.2s - 5.8s
   2    │    10s     │    8.5s - 11.5s
   3    │    20s     │   17.0s - 23.0s
   4    │    40s     │   34.0s - 46.0s
   5    │    80s     │   68.0s - 92.0s
```

### Linear Backoff

Same delay between all retries:

```csharp
.WithRetry(retry => retry
    .MaxRetries(3)
    .LinearBackoff(TimeSpan.FromSeconds(30))  // 30s, 30s, 30s
    .WithJitter()
    .ThenSendToDeadLetter())
```

## Jitter

Jitter adds random variation (±15%) to delays to prevent the "thundering herd" problem when many consumers retry simultaneously:

```csharp
// With jitter enabled (default)
.WithRetry(retry => retry
    .MaxRetries(3)
    .ExponentialBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), 2.0)
    .WithJitter())  // Adds ±15% variation

// Without jitter
.WithRetry(retry => retry
    .MaxRetries(3)
    .ExponentialBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), 2.0)
    .NoJitter())  // Exact delays
```

## Exhaustion Actions

When all retries are exhausted, RabbitX can:

### Send to Dead Letter Queue (Default)

```csharp
.WithRetry(retry => retry
    .MaxRetries(3)
    .ExponentialBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), 2.0)
    .ThenSendToDeadLetter())  // or .OnExhausted(RetryExhaustedAction.SendToDeadLetter)
```

**Requires:** Dead letter configuration on the consumer:

```csharp
.AddConsumer("OrderConsumer", con => con
    .FromQueue("orders.queue")
    .WithRetry(retry => retry.MaxRetries(3).ThenSendToDeadLetter())
    .WithDeadLetter(dlx => dlx
        .Exchange("orders.dlx")
        .Queue("orders.dlq")))
```

### Requeue

Put the message back in the queue for later processing:

```csharp
.WithRetry(retry => retry
    .MaxRetries(3)
    .ExponentialBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), 2.0)
    .ThenRequeue())  // or .OnExhausted(RetryExhaustedAction.Requeue)
```

**Warning:** This can cause infinite loops if the message always fails.

### Discard

Silently discard the message:

```csharp
.WithRetry(retry => retry
    .MaxRetries(3)
    .ExponentialBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), 2.0)
    .ThenDiscard())  // or .OnExhausted(RetryExhaustedAction.Discard)
```

**Use case:** Non-critical messages where data loss is acceptable.

## JSON Configuration

Configure retry via appsettings.json:

```json
{
  "RabbitX": {
    "Consumers": {
      "OrderConsumer": {
        "Queue": "orders.queue",
        "Exchange": "orders.exchange",
        "RoutingKey": "orders.created",
        "Retry": {
          "MaxRetries": 5,
          "DelaysInSeconds": [10, 30, 60, 120, 300],
          "UseJitter": true,
          "OnRetryExhausted": "SendToDeadLetter"
        },
        "DeadLetter": {
          "Exchange": "orders.dlx",
          "Queue": "orders.dlq"
        }
      }
    }
  }
}
```

### Exponential Backoff via JSON

```json
{
  "RabbitX": {
    "Consumers": {
      "OrderConsumer": {
        "Queue": "orders.queue",
        "Retry": {
          "MaxRetries": 5,
          "InitialDelaySeconds": 5,
          "MaxDelaySeconds": 300,
          "BackoffMultiplier": 2.0,
          "UseJitter": true,
          "OnRetryExhausted": "SendToDeadLetter"
        }
      }
    }
  }
}
```

## How Retry Works Internally

1. **Handler throws exception** → Message marked for retry
2. **Check retry count** → Compare against `MaxRetries`
3. **If retries remaining:**
   - Calculate delay using configured strategy
   - Apply jitter if enabled
   - Wait for delay duration
   - Redeliver message to handler
4. **If retries exhausted:**
   - Execute exhaustion action (DLQ, Requeue, or Discard)

```
┌─────────────┐     ┌──────────────┐     ┌─────────────────┐
│   Handler   │────▶│   Exception  │────▶│ Retry Available?│
└─────────────┘     └──────────────┘     └────────┬────────┘
                                                  │
                          ┌───────────────────────┼───────────────────────┐
                          │                       │                       │
                          ▼                       ▼                       ▼
                    ┌───────────┐          ┌───────────┐          ┌───────────┐
                    │  Yes      │          │    No     │          │    No     │
                    │  Retry    │          │   + DLQ   │          │ + Discard │
                    └─────┬─────┘          └─────┬─────┘          └─────┬─────┘
                          │                      │                      │
                          ▼                      ▼                      ▼
                    ┌───────────┐          ┌───────────┐          ┌───────────┐
                    │   Wait    │          │  Send to  │          │  Message  │
                    │ + Retry   │          │    DLQ    │          │ Discarded │
                    └───────────┘          └───────────┘          └───────────┘
```

## Best Practices

### 1. Choose Appropriate Retry Counts

```csharp
// Critical messages: More retries with longer delays
.WithRetry(retry => retry
    .MaxRetries(5)
    .ExponentialBackoff(
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(10),
        2.0)
    .WithJitter()
    .ThenSendToDeadLetter())

// Non-critical messages: Fewer retries
.WithRetry(retry => retry
    .MaxRetries(2)
    .LinearBackoff(TimeSpan.FromSeconds(5))
    .ThenDiscard())
```

### 2. Always Enable Jitter

```csharp
// Good: Prevents thundering herd
.WithRetry(retry => retry
    .MaxRetries(3)
    .ExponentialBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), 2.0)
    .WithJitter())
```

### 3. Configure DLQ for Investigation

```csharp
.AddConsumer("OrderConsumer", con => con
    .FromQueue("orders.queue")
    .WithRetry(retry => retry
        .MaxRetries(5)
        .ThenSendToDeadLetter())
    .WithDeadLetter(dlx => dlx
        .Exchange("orders.dlx")
        .RoutingKey("orders.failed")
        .Queue("orders.dlq")))
```

### 4. Log Retry Attempts in Handler

```csharp
public async Task<ConsumeResult> HandleAsync(
    MessageContext<OrderEvent> context,
    CancellationToken ct)
{
    _logger.LogInformation(
        "Processing order {OrderId}, Redelivered: {Redelivered}",
        context.Message.OrderId,
        context.Redelivered);

    // Processing logic...
}
```

### 5. Use Idempotent Handlers

Since messages may be redelivered, ensure your handlers can safely process the same message multiple times:

```csharp
public async Task<ConsumeResult> HandleAsync(
    MessageContext<OrderEvent> context,
    CancellationToken ct)
{
    // Check if already processed
    if (await _store.IsProcessedAsync(context.MessageId, ct))
    {
        _logger.LogInformation("Message already processed: {Id}", context.MessageId);
        return ConsumeResult.Ack;
    }

    // Process and mark as processed
    await ProcessOrderAsync(context.Message, ct);
    await _store.MarkProcessedAsync(context.MessageId, ct);

    return ConsumeResult.Ack;
}
```

## Configuration Examples

### High-Reliability Configuration

For critical business messages:

```csharp
.AddConsumer("PaymentConsumer", con => con
    .FromQueue("payments.queue")
    .BindToExchange("payments.exchange", "payment.process")
    .WithPrefetchCount(5)
    .WithRetry(retry => retry
        .MaxRetries(10)
        .ExponentialBackoff(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(30),
            2.0)
        .WithJitter()
        .ThenSendToDeadLetter())
    .WithDeadLetter(dlx => dlx
        .Exchange("payments.dlx")
        .RoutingKey("payment.failed")
        .Queue("payments.dlq")))
```

### Quick-Fail Configuration

For time-sensitive messages:

```csharp
.AddConsumer("AlertConsumer", con => con
    .FromQueue("alerts.queue")
    .BindToExchange("alerts.exchange", "alert.#", "topic")
    .WithPrefetchCount(20)
    .WithRetry(retry => retry
        .MaxRetries(2)
        .LinearBackoff(TimeSpan.FromSeconds(1))
        .ThenDiscard()))
```

### Balanced Configuration

For typical business events:

```csharp
.AddConsumer("OrderConsumer", con => con
    .FromQueue("orders.queue")
    .BindToExchange("orders.exchange", "orders.created")
    .WithPrefetchCount(10)
    .WithRetry(retry => retry
        .MaxRetries(5)
        .WithDelays(
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(120),
            TimeSpan.FromSeconds(300))
        .WithJitter()
        .ThenSendToDeadLetter())
    .WithDeadLetter(dlx => dlx
        .Exchange("orders.dlx")
        .Queue("orders.dlq")))
```

## See Also

- [Consumers](04-consumers.md) - Consumer configuration
- [Dead Letter Queues](07-dead-letter-queues.md) - Handling failed messages
- [Configuration](02-configuration.md) - All configuration options
- [Polly Documentation](https://github.com/App-vNext/Polly) - Advanced resilience patterns
