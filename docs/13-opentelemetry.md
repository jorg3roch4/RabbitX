# OpenTelemetry Instrumentation

RabbitX includes built-in OpenTelemetry support for **distributed tracing** and **metrics**, enabling full observability of message flows across publish, consume, and RPC operations.

## Overview

- **Tracing**: Activities (spans) are created for each publish, consume, and RPC operation with semantic attributes following [OpenTelemetry Messaging Conventions](https://opentelemetry.io/docs/specs/semconv/messaging/)
- **Metrics**: Counters and histograms track message throughput, latency, errors, retries, and connection health
- **Context Propagation**: W3C TraceContext (`traceparent`/`tracestate`) is automatically propagated through AMQP message headers
- **Zero overhead**: When no OTel SDK is configured, all instrumentation is effectively no-op (null checks only)

## Setup

### Install the OpenTelemetry SDK

RabbitX depends only on `OpenTelemetry.Api` (lightweight). To actually collect and export telemetry, add the SDK packages to your application:

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.Console        # or Jaeger, OTLP, etc.
dotnet add package OpenTelemetry.Exporter.Prometheus      # for metrics
```

### Register RabbitX Instrumentation

```csharp
using RabbitX.Diagnostics;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddRabbitXInstrumentation()     // Subscribe to RabbitX ActivitySource
        .AddOtlpExporter())             // Export to your collector
    .WithMetrics(metrics => metrics
        .AddRabbitXInstrumentation()     // Subscribe to RabbitX Meter
        .AddPrometheusExporter());       // Export metrics
```

That's it. No changes needed to your existing RabbitX configuration.

## Distributed Tracing

### Span Hierarchy

When a message is published and then consumed, the trace context is automatically propagated through AMQP headers:

```
[RabbitX] orders.exchange publish (Producer)     ← Publisher creates this span
  └─ [RabbitMQ.Client] basic.publish (Internal)  ← RabbitMQ.Client creates this automatically

--- W3C traceparent propagated via AMQP headers ---

[RabbitX] orders.queue process (Consumer)        ← Consumer creates this span, linked to Producer
  └─ [User Code] HandleAsync                     ← Your handler code
```

For RPC operations:

```
[RabbitX] rpc.exchange publish (Client)          ← RPC client span
  └─ [RabbitMQ.Client] basic.publish

--- headers ---

[RabbitX] rpc.queue process (Server)             ← RPC server span, linked to client
  └─ [User Code] HandleAsync
```

### Semantic Attributes

All spans include standard OpenTelemetry messaging attributes:

| Attribute | Description | Example |
|-----------|-------------|---------|
| `messaging.system` | Always `rabbitmq` | `rabbitmq` |
| `messaging.destination.name` | Exchange or queue name | `orders.exchange` |
| `messaging.operation.type` | `publish` or `process` | `publish` |
| `messaging.message.id` | Message or correlation ID | `a1b2c3d4-...` |
| `messaging.message.body.size` | Message body size in bytes | `256` |
| `messaging.rabbitmq.destination.routing_key` | Routing key | `order.created` |

RabbitX-specific attributes:

| Attribute | Description |
|-----------|-------------|
| `rabbitx.publisher.name` | Publisher name |
| `rabbitx.consumer.name` | Consumer name |
| `rabbitx.rpc.client.name` | RPC client name |
| `rabbitx.rpc.handler.name` | RPC handler name |
| `rabbitx.retry.count` | Current retry count (on consume) |
| `rabbitx.consume.result` | Handler result (`ack`, `retry`, `reject`, `defer`) |

### Error Recording

When exceptions occur, the span is marked with `Error` status and an `exception` event is recorded with:
- `exception.type`: Full type name
- `exception.message`: Exception message
- `exception.stacktrace`: Full stack trace

## Metrics

### Available Instruments

#### Publishing

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `rabbitx.messages.published` | Counter | messages | Total messages published successfully |
| `rabbitx.messages.publish.errors` | Counter | errors | Total publish errors |
| `rabbitx.messages.publish.duration` | Histogram | ms | Publish operation duration |
| `rabbitx.messages.publish.size` | Histogram | bytes | Published message body size |

#### Consuming

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `rabbitx.messages.consumed` | Counter | messages | Total messages received |
| `rabbitx.messages.consume.results` | Counter | messages | Results by type (tag: `result`) |
| `rabbitx.messages.consume.duration` | Histogram | ms | Message processing duration |
| `rabbitx.messages.consume.errors` | Counter | errors | Total unhandled consume errors |

#### Retry

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `rabbitx.messages.retries` | Counter | retries | Total retry attempts |
| `rabbitx.messages.retries.exhausted` | Counter | messages | Total retry exhaustions |

#### RPC

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `rabbitx.rpc.calls` | Counter | calls | Total RPC calls made |
| `rabbitx.rpc.duration` | Histogram | ms | RPC call duration |
| `rabbitx.rpc.timeouts` | Counter | timeouts | Total RPC timeouts |
| `rabbitx.rpc.errors` | Counter | errors | Total RPC errors |

#### Connection

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `rabbitx.connections.created` | Counter | connections | Total connections created |
| `rabbitx.connections.errors` | Counter | errors | Total connection errors |

### Metric Tags

All metrics include dimensional tags for filtering:

- **Publishing**: `publisher`, `exchange`
- **Consuming**: `consumer`, `queue`, `result` (for consume results)
- **RPC Client**: `client`, `exchange`
- **RPC Server**: `handler`, `queue`
- **Connection**: `host`, `vhost`

## Context Propagation

RabbitX automatically handles W3C TraceContext propagation:

1. **On publish**: The current `Activity.Context` is injected as `traceparent` and `tracestate` headers into the AMQP message
2. **On consume**: Headers are extracted to restore the parent context, creating a linked consumer span
3. **On retry**: The trace context is re-injected when republishing a message for retry, maintaining the trace chain

This works transparently with all message types: standard publish/consume, RPC, and retry flows.

## Example: Viewing Traces in Jaeger

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddRabbitXInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317")));

// Your existing RabbitX setup - no changes needed
builder.Services.AddRabbitX(rabbit => rabbit
    .ConfigureConnection(conn => conn.HostName = "localhost")
    .AddPublisher<OrderEvent>("orders", pub => pub
        .Exchange("orders.exchange")
        .RoutingKey("order.created"))
    .AddConsumer<OrderEvent>("orders", consumer => consumer
        .Queue("orders.queue")));
```

With this configuration, you'll see complete traces in Jaeger showing the full message lifecycle from publisher to consumer, including retry attempts and RPC round-trips.
