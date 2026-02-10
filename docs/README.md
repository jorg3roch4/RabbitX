# RabbitX Documentation

**RabbitX** is a modern, lightweight RabbitMQ client library for .NET 10 that simplifies messaging patterns with a fluent API, built-in resilience, and first-class support for RPC (Request-Reply) operations.

## Features

- **Fluent Configuration API** - Clean, readable configuration in code or appsettings.json
- **Publisher Confirms** - Reliable message delivery with confirmation support
- **Consumer Management** - Automatic consumer lifecycle with `IHostedService`
- **RPC Pattern** - Sync-Async-Sync request-reply with timeout handling
- **Retry & Resilience** - Built-in Polly integration for fault tolerance
- **Dead Letter Queues** - Automatic DLQ configuration for failed messages
- **JSON Serialization** - System.Text.Json with customizable options
- **Health Checks** - Built-in ASP.NET Core health check with blocked state detection
- **OpenTelemetry** - Distributed tracing, metrics, and W3C TraceContext propagation

## Requirements

- .NET 10.0 or later
- RabbitMQ 3.12+ (recommended)

## Quick Start

```csharp
// Install: dotnet add package RabbitX

services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddPublisher("OrderPublisher", pub => pub
        .ToExchange("orders.events.exchange", "topic")
        .WithRoutingKey("orders.created"))
    .AddConsumer("OrderConsumer", con => con
        .FromQueue("orders.created.queue")
        .BindToExchange("orders.events.exchange", "orders.created")));

// Register handler and hosted consumer
services.AddMessageHandler<OrderCreatedEvent, OrderCreatedHandler>();
services.AddHostedConsumer<OrderCreatedEvent>("OrderConsumer");
```

## Table of Contents

| # | Topic | Description |
|---|-------|-------------|
| 1 | [Getting Started](01-getting-started.md) | Installation, requirements, and your first message |
| 2 | [Configuration](02-configuration.md) | Fluent API and appsettings.json configuration |
| 3 | [Publishers](03-publishers.md) | Publishing messages with optional confirms |
| 4 | [Consumers](04-consumers.md) | Consuming messages with handlers |
| 5 | [RPC (Request-Reply)](05-rpc.md) | Sync-Async-Sync pattern implementation |
| 6 | [Retry & Resilience](06-retry-resilience.md) | Polly integration for fault tolerance |
| 7 | [Dead Letter Queues](07-dead-letter-queues.md) | Handling failed messages |
| 8 | [Serialization](08-serialization.md) | JSON and custom serializers |
| 9 | [Naming Conventions](09-naming-conventions.md) | Best practices for naming |
| 10 | [Samples](10-samples.md) | Complete working examples |
| 11 | [Troubleshooting](11-troubleshooting.md) | Common issues and solutions |
| 12 | [Health Checks](12-health-checks.md) | ASP.NET Core health check integration |
| 13 | [OpenTelemetry](13-opentelemetry.md) | Distributed tracing, metrics, and context propagation |

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Your Application                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                   │
│  │  Publishers  │    │  Consumers   │    │  RPC Client  │                   │
│  │              │    │              │    │              │                   │
│  │ IPublisher   │    │ IConsumer    │    │ IRpcClient   │                   │
│  │ Factory      │    │ Factory      │    │ Factory      │                   │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘                   │
│         │                   │                   │                           │
│         └───────────────────┼───────────────────┘                           │
│                             │                                               │
│                    ┌────────▼────────┐                                      │
│                    │  RabbitX Core   │                                      │
│                    │                 │                                      │
│                    │ • Connection    │                                      │
│                    │ • Serialization │                                      │
│                    │ • Resilience    │                                      │
│                    └────────┬────────┘                                      │
│                             │                                               │
└─────────────────────────────┼───────────────────────────────────────────────┘
                              │
                    ┌─────────▼─────────┐
                    │     RabbitMQ      │
                    │                   │
                    │ Exchanges, Queues │
                    │ Bindings, etc.    │
                    └───────────────────┘
```

## Sample Projects

The `/samples` directory contains complete working examples:

| Sample | Description |
|--------|-------------|
| `RabbitX.Sample` | Basic pub/sub and consumer patterns |
| `RabbitX.Sample.Rpc` | RPC pattern with Calculator and User Query |

## External Resources

- [RabbitMQ Official Documentation](https://www.rabbitmq.com/docs)
- [RabbitMQ Tutorials](https://www.rabbitmq.com/tutorials)
- [AMQP 0-9-1 Protocol](https://www.rabbitmq.com/tutorials/amqp-concepts)

## License

MIT License - See [LICENSE](../LICENSE) for details.
