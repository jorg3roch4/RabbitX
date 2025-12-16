# Naming Conventions

Consistent naming conventions for queues, exchanges, and routing keys make your RabbitMQ infrastructure easier to understand, monitor, and maintain.

## General Principles

1. **Use lowercase** - All names should be lowercase
2. **Use dots as separators** - `shop.orders.created` not `shop_orders_created`
3. **Be descriptive** - Names should convey purpose
4. **Follow hierarchy** - Domain → Service → Action
5. **Include type suffix** - `.queue`, `.exchange`, `.dlq`

## Queue Naming

### Pattern

```
{domain}.{service}.{action}.queue
```

### Components

| Component | Description | Examples |
|-----------|-------------|----------|
| `{domain}` | Business domain or bounded context | `shop`, `billing`, `inventory`, `notifications` |
| `{service}` | Service or entity being processed | `orders`, `payments`, `users`, `products` |
| `{action}` | The action or event type | `created`, `updated`, `deleted`, `process` |
| `.queue` | Type suffix (optional but recommended) | `.queue` |

### Examples

```
shop.orders.created.queue          # New orders to process
shop.orders.cancelled.queue        # Cancelled orders
billing.invoices.generate.queue    # Invoice generation requests
inventory.products.update.queue    # Product updates
notifications.email.send.queue     # Email sending queue
```

### Consumer Queue Naming

When a service consumes events from another domain, prefix with the consumer:

```
{consumer-service}.{source-domain}.{event}.queue
```

**Example:** Inventory service consuming order events:
```
inventory.shop-orders.created.queue
shipping.shop-orders.confirmed.queue
```

## Exchange Naming

### Pattern

```
{domain}.{entity}.exchange
```

### Examples

```
shop.orders.exchange           # Order-related events
billing.invoices.exchange      # Invoice events
inventory.products.exchange    # Product events
notifications.alerts.exchange  # Alert notifications
```

### Exchange Type in Name (Optional)

For clarity, you can include the exchange type:

```
shop.orders.topic.exchange     # Topic exchange
shop.orders.fanout.exchange    # Fanout exchange
shop.orders.direct.exchange    # Direct exchange
```

## Routing Key Naming

### Pattern

```
{entity}.{action}[.{qualifier}]
```

### Topic Exchange Patterns

Use hierarchical keys for topic exchanges:

```
orders.created                 # All new orders
orders.created.priority        # Priority orders
orders.cancelled               # Cancelled orders
orders.shipped.domestic        # Domestic shipments
orders.shipped.international   # International shipments
```

### Wildcard Matching

| Pattern | Matches |
|---------|---------|
| `orders.*` | `orders.created`, `orders.cancelled` |
| `orders.#` | `orders.created`, `orders.shipped.domestic` |
| `#.priority` | `orders.created.priority`, `alerts.priority` |

### Direct Exchange Keys

For direct exchanges, use specific keys:

```
orders.process
users.validate
products.sync
```

## Dead Letter Queue Naming

### Pattern

```
{original-queue-name}.dlq
```

Or:

```
{domain}.{service}.dead-letters.queue
```

### Examples

```
shop.orders.created.dlq                    # DLQ for order creation
shop.orders.dead-letters.queue             # Alternative format
billing.invoices.dead-letters.queue        # Invoice DLQ
```

### Dead Letter Exchange

```
{domain}.dlx.exchange
```

**Example:**
```
shop.dlx.exchange
billing.dlx.exchange
```

## RPC Naming

### RPC Queue Pattern

```
{domain}.{service}.{operation}.queue
```

### RPC Exchange Pattern

```
{domain}.rpc.exchange
```

### Examples

```csharp
// Calculator RPC
.AddRpcClient<CalculateRequest, CalculateResponse>("Calculator", rpc => rpc
    .ToExchange("math.rpc.exchange", "direct")
    .WithRoutingKey("math.calculator.compute"))

.AddRpcHandler<CalculateRequest, CalculateResponse, CalculatorHandler>("Handler", h => h
    .FromQueue("math.calculator.compute.queue")
    .BindToExchange("math.rpc.exchange", "math.calculator.compute"))

// User lookup RPC
.AddRpcClient<GetUserRequest, GetUserResponse>("UserLookup", rpc => rpc
    .ToExchange("users.rpc.exchange", "direct")
    .WithRoutingKey("users.accounts.get-by-id"))

.AddRpcHandler<GetUserRequest, GetUserResponse, UserLookupHandler>("Handler", h => h
    .FromQueue("users.accounts.get-by-id.queue")
    .BindToExchange("users.rpc.exchange", "users.accounts.get-by-id"))
```

## Complete Example

Here's a full example for an e-commerce system:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        E-Commerce Naming Example                             │
│                                                                              │
│  EXCHANGES:                                                                  │
│  ──────────                                                                  │
│  shop.orders.exchange         (topic)   - Order lifecycle events            │
│  shop.products.exchange       (topic)   - Product updates                   │
│  billing.invoices.exchange    (direct)  - Invoice processing                │
│  notifications.exchange       (fanout)  - System-wide notifications         │
│  shop.dlx.exchange            (direct)  - Dead letter exchange              │
│                                                                              │
│  QUEUES:                                                                     │
│  ───────                                                                     │
│  shop.orders.created.queue              - New order processing              │
│  shop.orders.cancelled.queue            - Order cancellation                │
│  shop.orders.dead-letters.queue         - Failed order messages             │
│                                                                              │
│  inventory.shop-orders.created.queue    - Inventory reservation             │
│  shipping.shop-orders.confirmed.queue   - Shipping preparation              │
│  billing.shop-orders.completed.queue    - Invoice generation                │
│                                                                              │
│  notifications.email.send.queue         - Email delivery                    │
│  notifications.sms.send.queue           - SMS delivery                      │
│                                                                              │
│  ROUTING KEYS:                                                               │
│  ─────────────                                                               │
│  orders.created                         - New orders                        │
│  orders.created.priority                - Priority orders                   │
│  orders.confirmed                       - Confirmed orders                  │
│  orders.shipped                         - Shipped orders                    │
│  orders.cancelled                       - Cancelled orders                  │
│  products.updated                       - Product updates                   │
│  products.deleted                       - Product deletions                 │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Configuration Example

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")

    // Publishers
    .AddPublisher<OrderCreated>("OrderCreatedPublisher", pub => pub
        .ToExchange("shop.orders.exchange", "topic")
        .WithRoutingKey("orders.created")
        .EnablePublisherConfirms())

    .AddPublisher<OrderCancelled>("OrderCancelledPublisher", pub => pub
        .ToExchange("shop.orders.exchange", "topic")
        .WithRoutingKey("orders.cancelled")
        .EnablePublisherConfirms())

    // Consumers
    .AddConsumer<OrderCreated, OrderCreatedHandler>("OrderProcessor", con => con
        .FromQueue("shop.orders.created.queue")
        .BindToExchange("shop.orders.exchange", "orders.created")
        .WithDeadLetter(dlq => dlq
            .ToExchange("shop.dlx.exchange")
            .ToQueue("shop.orders.dead-letters.queue")))

    // Cross-domain consumer
    .AddConsumer<OrderCreated, InventoryReservationHandler>("InventoryReservation", con => con
        .FromQueue("inventory.shop-orders.created.queue")
        .BindToExchange("shop.orders.exchange", "orders.created")
        .WithPrefetchCount(10)));
```

## Environment Prefixes

For multi-environment deployments, consider environment prefixes:

```
# Development
dev.shop.orders.created.queue
dev.shop.orders.exchange

# Staging
staging.shop.orders.created.queue
staging.shop.orders.exchange

# Production (no prefix or explicit)
prod.shop.orders.created.queue
shop.orders.created.queue
```

### Configuration with Environment

```csharp
var environment = builder.Environment.EnvironmentName.ToLower();

services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddPublisher<OrderCreated>("Orders", pub => pub
        .ToExchange($"{environment}.shop.orders.exchange", "topic")
        .WithRoutingKey("orders.created")));
```

## Anti-Patterns to Avoid

### 1. Generic Names

```csharp
// Bad: Too generic
.FromQueue("queue1")
.ToExchange("exchange")
.WithRoutingKey("message")

// Good: Descriptive
.FromQueue("shop.orders.process.queue")
.ToExchange("shop.orders.exchange")
.WithRoutingKey("orders.created")
```

### 2. Inconsistent Separators

```csharp
// Bad: Mixed separators
.FromQueue("shop_orders-created.queue")

// Good: Consistent dots
.FromQueue("shop.orders.created.queue")
```

### 3. Missing Context

```csharp
// Bad: No domain context
.FromQueue("created.queue")

// Good: Full context
.FromQueue("shop.orders.created.queue")
```

### 4. Hardcoded Values

```csharp
// Bad: Hardcoded everywhere
await channel.QueueDeclareAsync("shop.orders.queue");
await channel.ExchangeDeclareAsync("shop.orders.exchange");

// Good: Use constants or configuration
public static class QueueNames
{
    public const string OrdersCreated = "shop.orders.created.queue";
    public const string OrdersCancelled = "shop.orders.cancelled.queue";
}

public static class ExchangeNames
{
    public const string ShopOrders = "shop.orders.exchange";
}
```

## Naming Checklist

Before deploying, verify your naming:

- [ ] All names are lowercase
- [ ] Dots used as separators
- [ ] Domain/service/action hierarchy followed
- [ ] Type suffix included (.queue, .exchange)
- [ ] DLQ names match source queues
- [ ] Routing keys use proper hierarchy
- [ ] Cross-service queues include consumer prefix
- [ ] Environment prefix if needed
- [ ] No generic or ambiguous names

## See Also

- [Configuration](02-configuration.md) - Setting up queues and exchanges
- [Publishers](03-publishers.md) - Exchange and routing key configuration
- [Consumers](04-consumers.md) - Queue configuration
- [Dead Letter Queues](07-dead-letter-queues.md) - DLQ naming
- [RabbitMQ Naming Best Practices](https://www.rabbitmq.com/docs/queues#names) - Official guidance
