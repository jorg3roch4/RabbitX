# Configuration

RabbitX supports two configuration approaches: **Fluent API** and **appsettings.json**. Both approaches are fully equivalent and can be combined for maximum flexibility.

## Configuration Methods

| Method | Best For | Features |
|--------|----------|----------|
| **Fluent API** | Compile-time validation, IntelliSense | Type-safe, IDE support, refactoring-friendly |
| **appsettings.json** | Environment-specific config, no recompilation | Easy deployment, secrets management |
| **Hybrid** | Best of both worlds | Load base from file, override in code |

---

## Fluent API Configuration

### Complete Example

```csharp
services.AddRabbitX(options => options
    // Connection
    .UseConnection("localhost", 5672)
    .UseCredentials("rabbit", "rabbit")
    .UseVirtualHost("/")
    .EnableAutoRecovery(TimeSpan.FromSeconds(10))
    .WithConnectionTimeout(TimeSpan.FromSeconds(30))
    .WithHeartbeat(TimeSpan.FromSeconds(60))
    .WithClientName("MyApplication")

    // Publisher
    .AddPublisher("OrderPublisher", pub => pub
        .ToExchange("orders.exchange", "direct")
        .WithRoutingKey("order.created")
        .Durable()
        .Persistent()
        .AutoDelete(false)
        .Mandatory(false)
        .WithConfirmTimeout(TimeSpan.FromSeconds(5)))

    // Consumer with retry and dead letter
    .AddConsumer("OrderConsumer", con => con
        .FromQueue("orders.queue")
        .BindToExchange("orders.exchange", "order.created", "direct")
        .Durable()
        .Exclusive(false)
        .AutoDelete(false)
        .WithPrefetchCount(10)
        .WithPrefetchSize(0)
        .WithGlobalQos(false)
        .WithMessageTtl(TimeSpan.FromHours(24))
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
            .RoutingKey("order.failed")
            .Queue("orders.dlq")
            .WithExchangeType("direct")
            .Durable())));

// Register message handlers
services.AddMessageHandler<OrderMessage, OrderMessageHandler>();

// Register consumers as hosted services
services.AddHostedConsumer<OrderMessage>("OrderConsumer");
```

### Connection Options

| Method | Description | Default |
|--------|-------------|---------|
| `UseConnection(host, port)` | RabbitMQ server address | `localhost:5672` |
| `UseCredentials(user, pass)` | Authentication credentials | `guest/guest` |
| `UseVirtualHost(vhost)` | Virtual host to connect to | `/` |
| `EnableAutoRecovery(interval?)` | Enable automatic connection recovery | Enabled, 10s |
| `DisableAutoRecovery()` | Disable automatic connection recovery | - |
| `WithConnectionTimeout(timeout)` | Connection timeout | 30s |
| `WithHeartbeat(interval)` | Heartbeat interval | 60s |
| `WithClientName(name)` | Client name shown in RabbitMQ management | - |

```csharp
.UseConnection("rabbitmq.mycompany.com", 5672)
.UseCredentials("app_user", "secure_password")
.UseVirtualHost("/production")
.EnableAutoRecovery(TimeSpan.FromSeconds(15))
.WithConnectionTimeout(TimeSpan.FromSeconds(60))
.WithHeartbeat(TimeSpan.FromSeconds(30))
.WithClientName("OrderService.Worker")
```

### Publisher Options

| Method | Description | Default |
|--------|-------------|---------|
| `ToExchange(name, type)` | Target exchange and type | Required |
| `WithRoutingKey(key)` | Routing key for messages | `""` |
| `Durable(bool)` | Exchange survives broker restart | `true` |
| `AutoDelete(bool)` | Delete exchange when unused | `false` |
| `Persistent(bool)` | Messages persist to disk | `true` |
| `Mandatory(bool)` | Return unroutable messages | `false` |
| `WithConfirmTimeout(timeout)` | Publisher confirm timeout | 5s |
| `WithArgument(key, value)` | Custom exchange argument | - |
| `WithArguments(dict)` | Multiple custom arguments | - |

```csharp
.AddPublisher("PaymentPublisher", pub => pub
    .ToExchange("payments.exchange", "topic")
    .WithRoutingKey("payment.processed")
    .Durable()
    .Persistent()
    .Mandatory()
    .WithConfirmTimeout(TimeSpan.FromSeconds(10))
    .WithArgument("x-custom", "value"))
```

### Consumer Options

| Method | Description | Default |
|--------|-------------|---------|
| `FromQueue(name)` | Queue to consume from | Required |
| `BindToExchange(exchange, routingKey, type)` | Bind queue to exchange | - |
| `Durable(bool)` | Queue survives broker restart | `true` |
| `Exclusive(bool)` | Queue exclusive to connection | `false` |
| `AutoDelete(bool)` | Delete queue when unused | `false` |
| `WithPrefetchCount(count)` | Messages prefetched | `1` |
| `WithPrefetch(count)` | Alias for WithPrefetchCount | `1` |
| `WithPrefetchSize(bytes)` | Prefetch size in bytes | `0` (unlimited) |
| `WithGlobalQos(bool)` | Apply QoS to channel vs consumer | `false` |
| `WithQos(configure)` | Configure QoS with builder | - |
| `WithMessageTtl(ttl)` | Message time-to-live | - |
| `WithRetry(configure)` | Configure retry policy | - |
| `WithMaxRetries(count)` | Quick max retries setting | `3` |
| `WithDeadLetter(configure)` | Configure dead letter exchange | - |
| `WithDeadLetter(exchange, routingKey, queue)` | Quick DLX setup | - |
| `WithArgument(key, value)` | Custom queue argument | - |
| `WithArguments(dict)` | Multiple custom arguments | - |

```csharp
.AddConsumer("EmailConsumer", con => con
    .FromQueue("emails.queue")
    .BindToExchange("emails.exchange", "email.#", "topic")
    .Durable()
    .WithPrefetchCount(5)
    .WithPrefetchSize(1024)
    .WithGlobalQos(false)
    .WithMessageTtl(TimeSpan.FromHours(1))
    .WithRetry(retry => retry
        .MaxRetries(3)
        .ExponentialBackoff(
            initialDelay: TimeSpan.FromSeconds(5),
            maxDelay: TimeSpan.FromMinutes(5),
            multiplier: 2.0)
        .WithJitter()
        .ThenSendToDeadLetter())
    .WithDeadLetter("emails.dlx", "email.failed", "emails.dlq"))
```

### Retry Options

| Method | Description | Default |
|--------|-------------|---------|
| `MaxRetries(count)` | Maximum retry attempts | `3` |
| `NoRetries()` | Disable retries | - |
| `WithDelays(timeSpans...)` | Specific delay for each retry | - |
| `WithDelaysInSeconds(ints...)` | Specific delays in seconds | - |
| `ExponentialBackoff(initial, max, multiplier)` | Exponential backoff strategy | - |
| `LinearBackoff(delay)` | Constant delay between retries | - |
| `WithJitter(bool)` | Add random variation to delays | `true` |
| `NoJitter()` | Disable jitter | - |
| `OnExhausted(action)` | Action when retries exhausted | `SendToDeadLetter` |
| `ThenSendToDeadLetter()` | Send to DLX when exhausted | - |
| `ThenRequeue()` | Requeue when exhausted | - |
| `ThenDiscard()` | Discard when exhausted | - |

#### Specific Delays Example

```csharp
.WithRetry(retry => retry
    .WithDelays(
        TimeSpan.FromSeconds(10),   // 1st retry
        TimeSpan.FromSeconds(30),   // 2nd retry
        TimeSpan.FromSeconds(60),   // 3rd retry
        TimeSpan.FromSeconds(120),  // 4th retry
        TimeSpan.FromSeconds(300))  // 5th retry
    .WithJitter()
    .ThenSendToDeadLetter())
```

#### Exponential Backoff Example

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

### Dead Letter Options

| Method | Description | Default |
|--------|-------------|---------|
| `Exchange(name)` | Dead letter exchange name | Required |
| `RoutingKey(key)` | Dead letter routing key | `""` |
| `Queue(name)` | Dead letter queue name | Required |
| `WithExchangeType(type)` | DLX exchange type | `direct` |
| `Durable(bool)` | DLX survives broker restart | `true` |

```csharp
.WithDeadLetter(dlx => dlx
    .Exchange("myapp.dlx")
    .RoutingKey("failed.orders")
    .Queue("myapp.failed.orders")
    .WithExchangeType("direct")
    .Durable())
```

---

## appsettings.json Configuration

### Complete Example

```json
{
  "RabbitX": {
    "Connection": {
      "HostName": "localhost",
      "Port": 5672,
      "UserName": "rabbit",
      "Password": "rabbit",
      "VirtualHost": "/",
      "AutomaticRecoveryEnabled": true,
      "NetworkRecoveryIntervalSeconds": 10,
      "ConnectionTimeoutSeconds": 30,
      "RequestedHeartbeatSeconds": 60,
      "ClientProvidedName": "MyApplication"
    },
    "Publishers": {
      "OrderPublisher": {
        "Exchange": "orders.exchange",
        "ExchangeType": "direct",
        "RoutingKey": "order.created",
        "Durable": true,
        "AutoDelete": false,
        "Persistent": true,
        "Mandatory": false,
        "ConfirmTimeoutMs": 5000
      }
    },
    "Consumers": {
      "OrderConsumer": {
        "Queue": "orders.queue",
        "Exchange": "orders.exchange",
        "ExchangeType": "direct",
        "RoutingKey": "order.created",
        "Durable": true,
        "Exclusive": false,
        "AutoDelete": false,
        "MessageTtlSeconds": 86400,
        "Qos": {
          "PrefetchSize": 0,
          "PrefetchCount": 10,
          "Global": false
        },
        "Retry": {
          "MaxRetries": 5,
          "DelaysInSeconds": [10, 30, 60, 120, 300],
          "UseJitter": true,
          "OnRetryExhausted": "SendToDeadLetter"
        },
        "DeadLetter": {
          "Exchange": "orders.dlx",
          "RoutingKey": "order.failed",
          "Queue": "orders.dlq",
          "ExchangeType": "direct",
          "Durable": true
        }
      }
    },
    "RpcClients": {
      "ProductRpc": {
        "Exchange": "catalog.rpc.exchange",
        "ExchangeType": "direct",
        "RoutingKey": "catalog.products.get",
        "TimeoutSeconds": 30,
        "UseDirectReplyTo": true,
        "DeclareExchange": true,
        "Durable": true
      }
    },
    "RpcHandlers": {
      "ProductRpcHandler": {
        "Queue": "catalog.products.get.queue",
        "Exchange": "catalog.rpc.exchange",
        "ExchangeType": "direct",
        "RoutingKey": "catalog.products.get",
        "DeclareQueue": true,
        "Durable": true,
        "PrefetchCount": 20
      }
    }
  }
}
```

### Loading from Configuration

```csharp
// Method 1: Direct from IConfiguration
services.AddRabbitX(configuration);

// Method 2: From specific section
services.AddRabbitX(configuration, "RabbitX");

// Method 3: With Fluent API (for handlers registration)
services.AddRabbitX(configuration);
services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
services.AddHostedConsumer<OrderMessage>("OrderConsumer");
```

### Connection Options Reference

| JSON Property | Type | Default | Description |
|---------------|------|---------|-------------|
| `HostName` | string | `localhost` | RabbitMQ server hostname |
| `Port` | int | `5672` | RabbitMQ server port |
| `UserName` | string | `guest` | Authentication username |
| `Password` | string | `guest` | Authentication password |
| `VirtualHost` | string | `/` | Virtual host to connect to |
| `AutomaticRecoveryEnabled` | bool | `true` | Enable automatic connection recovery |
| `NetworkRecoveryIntervalSeconds` | int | `10` | Seconds between recovery attempts |
| `ConnectionTimeoutSeconds` | int | `30` | Connection timeout in seconds |
| `RequestedHeartbeatSeconds` | int | `60` | Heartbeat interval in seconds |
| `ClientProvidedName` | string | null | Connection name in RabbitMQ management |

### Publisher Options Reference

| JSON Property | Type | Default | Description |
|---------------|------|---------|-------------|
| `Exchange` | string | Required | Target exchange name |
| `ExchangeType` | string | `direct` | Exchange type: direct, topic, fanout, headers |
| `RoutingKey` | string | `""` | Default routing key |
| `Durable` | bool | `true` | Exchange survives broker restart |
| `AutoDelete` | bool | `false` | Delete exchange when unused |
| `Persistent` | bool | `true` | Messages persist to disk |
| `Mandatory` | bool | `false` | Return unroutable messages |
| `ConfirmTimeoutMs` | int | `5000` | Publisher confirm timeout in ms |

### Consumer Options Reference

| JSON Property | Type | Default | Description |
|---------------|------|---------|-------------|
| `Queue` | string | Required | Queue to consume from |
| `Exchange` | string | null | Exchange to bind to |
| `ExchangeType` | string | `direct` | Exchange type for binding |
| `RoutingKey` | string | null | Binding routing key |
| `Durable` | bool | `true` | Queue survives broker restart |
| `Exclusive` | bool | `false` | Queue exclusive to connection |
| `AutoDelete` | bool | `false` | Delete queue when unused |
| `MessageTtlSeconds` | int | null | Message time-to-live in seconds |

### QoS Options Reference

| JSON Property | Type | Default | Description |
|---------------|------|---------|-------------|
| `PrefetchSize` | int | `0` | Prefetch size in bytes (0 = unlimited) |
| `PrefetchCount` | int | `1` | Number of messages to prefetch |
| `Global` | bool | `false` | Apply QoS to channel vs consumer |

### Retry Options Reference

| JSON Property | Type | Default | Description |
|---------------|------|---------|-------------|
| `MaxRetries` | int | `3` | Maximum retry attempts |
| `DelaysInSeconds` | int[] | null | Specific delay for each retry |
| `InitialDelaySeconds` | int | `5` | Initial delay for exponential backoff |
| `MaxDelaySeconds` | int | `300` | Maximum delay for exponential backoff |
| `BackoffMultiplier` | double | `2.0` | Multiplier for exponential backoff |
| `UseJitter` | bool | `true` | Add random variation to delays |
| `OnRetryExhausted` | string | `SendToDeadLetter` | Action: SendToDeadLetter, Requeue, Discard |

### Dead Letter Options Reference

| JSON Property | Type | Default | Description |
|---------------|------|---------|-------------|
| `Exchange` | string | Required | Dead letter exchange name |
| `RoutingKey` | string | `""` | Dead letter routing key |
| `Queue` | string | Required | Dead letter queue name |
| `ExchangeType` | string | `direct` | DLX exchange type |
| `Durable` | bool | `true` | DLX survives broker restart |

### RPC Client Options Reference

| JSON Property | Type | Default | Description |
|---------------|------|---------|-------------|
| `Name` | string | - | RPC client name (set automatically) |
| `Exchange` | string | Required | Exchange to publish RPC requests to |
| `ExchangeType` | string | `direct` | Exchange type: direct, topic, fanout, headers |
| `RoutingKey` | string | Required | Routing key for RPC requests |
| `TimeoutSeconds` | int | `30` | Default timeout for RPC calls in seconds |
| `UseDirectReplyTo` | bool | `true` | Use RabbitMQ's Direct Reply-to feature |
| `ReplyQueuePrefix` | string | null | Prefix for custom reply queue names |
| `DeclareExchange` | bool | `true` | Whether to declare the exchange on first use |
| `Durable` | bool | `true` | Whether the exchange is durable |

### RPC Handler Options Reference

| JSON Property | Type | Default | Description |
|---------------|------|---------|-------------|
| `Name` | string | - | RPC handler name (set automatically) |
| `Queue` | string | Required | Queue to consume RPC requests from |
| `Exchange` | string | null | Exchange to bind the queue to (optional) |
| `ExchangeType` | string | `direct` | Exchange type for binding |
| `RoutingKey` | string | null | Routing key for queue binding (optional) |
| `DeclareQueue` | bool | `true` | Whether to declare the queue on startup |
| `Durable` | bool | `true` | Whether the queue is durable |
| `PrefetchCount` | ushort | `10` | Prefetch count for RPC consumer |

---

## Hybrid Configuration

Combine appsettings.json with Fluent API overrides:

```csharp
// Load base configuration from file
services.AddRabbitX(options => options
    .LoadFromConfiguration(configuration.GetSection("RabbitX"))

    // Override specific settings
    .WithClientName($"OrderService-{Environment.MachineName}")

    // Modify existing consumer
    .ConfigureConsumer("OrderConsumer", con => con
        .WithPrefetchCount(20)
        .WithMaxRetries(10)));
```

---

## Environment-Specific Configuration

### appsettings.Development.json

```json
{
  "RabbitX": {
    "Connection": {
      "HostName": "localhost",
      "UserName": "guest",
      "Password": "guest",
      "ClientProvidedName": "MyApp-Development"
    }
  }
}
```

### appsettings.Production.json

```json
{
  "RabbitX": {
    "Connection": {
      "HostName": "rabbitmq.production.internal",
      "Port": 5672,
      "UserName": "app_service",
      "Password": "${RABBITMQ_PASSWORD}",
      "VirtualHost": "/production",
      "AutomaticRecoveryEnabled": true,
      "NetworkRecoveryIntervalSeconds": 5,
      "ClientProvidedName": "MyApp-Production"
    }
  }
}
```

---

## Configuration Comparison

### Fluent API vs appsettings.json

| Feature | Fluent API | appsettings.json |
|---------|------------|------------------|
| `UseConnection(host, port)` | `Connection.HostName`, `Connection.Port` |
| `UseCredentials(user, pass)` | `Connection.UserName`, `Connection.Password` |
| `UseVirtualHost(vhost)` | `Connection.VirtualHost` |
| `EnableAutoRecovery(interval)` | `AutomaticRecoveryEnabled`, `NetworkRecoveryIntervalSeconds` |
| `WithConnectionTimeout(ts)` | `Connection.ConnectionTimeoutSeconds` |
| `WithHeartbeat(ts)` | `Connection.RequestedHeartbeatSeconds` |
| `WithClientName(name)` | `Connection.ClientProvidedName` |
| `WithPrefetchCount(n)` | `Qos.PrefetchCount` |
| `WithPrefetchSize(n)` | `Qos.PrefetchSize` |
| `WithGlobalQos(bool)` | `Qos.Global` |
| `WithRetry(...)` | `Retry { ... }` |
| `WithDeadLetter(...)` | `DeadLetter { ... }` |

### RPC Client

| Fluent API | appsettings.json |
|------------|------------------|
| `AddRpcClient<TReq, TRes>("name", ...)` | `RpcClients.{name}` |
| `ToExchange(name, type)` | `Exchange`, `ExchangeType` |
| `WithRoutingKey(key)` | `RoutingKey` |
| `WithTimeout(ts)` | `TimeoutSeconds` |
| `UseDirectReplyTo(bool)` | `UseDirectReplyTo` |
| `WithReplyQueuePrefix(prefix)` | `ReplyQueuePrefix` |
| `DeclareExchange(bool)` | `DeclareExchange` |
| `NonDurable()` | `Durable: false` |

### RPC Handler

| Fluent API | appsettings.json |
|------------|------------------|
| `AddRpcHandler<TReq, TRes, THandler>("name", ...)` | `RpcHandlers.{name}` |
| `FromQueue(name)` | `Queue` |
| `BindToExchange(exchange, routingKey)` | `Exchange`, `RoutingKey` |
| `WithPrefetchCount(n)` | `PrefetchCount` |
| `DeclareQueue(bool)` | `DeclareQueue` |
| `NonDurable()` | `Durable: false` |

---

## See Also

- [Getting Started](01-getting-started.md) - Quick start guide
- [Publishers](03-publishers.md) - Detailed publisher configuration
- [Consumers](04-consumers.md) - Detailed consumer configuration
- [Retry & Resilience](06-retry-resilience.md) - Retry policies in depth
- [Dead Letter Queues](07-dead-letter-queues.md) - DLX configuration
