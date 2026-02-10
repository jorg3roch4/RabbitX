# Changelog

All notable changes to RabbitX will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

---

## [1.1.0] - 2026-02-01

### Added
- Health check support for ASP.NET Core (`IHealthCheck`)
  - `RabbitMQHealthCheck` - Verifies RabbitMQ connection status
  - `RabbitMQHealthCheckOptions` - Configurable name, tags, timeout, and failure status
  - `AddRabbitX()` extension method for `IHealthChecksBuilder`
- `IsBlocked` property on `IRabbitMQConnection` interface
  - Tracks connection blocked state from broker flow control
  - Returns `Degraded` health status when blocked

### Health Check Features
- **Fast-fail**: Checks `IsConnected` first for immediate response
- **Blocked detection**: Reports `Degraded` when connection is blocked by broker
- **Real verification**: Creates temporary channel to confirm actual communication
- **Rich data**: Returns host, port, virtualHost, clientName, server, version, and isBlocked

### Dependencies
- Added `Microsoft.Extensions.Diagnostics.HealthChecks` 10.0.1

---

## [1.0.0] - 2025-12-15

### Added
- Initial release of RabbitX
- Core abstractions for RabbitMQ messaging
  - `IRabbitMQConnection` - Connection lifecycle management
  - `IMessagePublisher<T>` - Fire-and-forget message publishing
  - `IReliableMessagePublisher<T>` - Publishing with broker confirmations
  - `IMessageConsumer<T>` - Type-safe message consumption
  - `IMessageHandler<T>` - Message handler interface
  - `IMessageSerializer` - Pluggable serialization (default: JSON)
- Fluent configuration API via `RabbitXOptionsBuilder`
  - `AddPublisher()` - Configure named publishers
  - `AddConsumer()` - Configure named consumers
  - `AddRpcClient<TRequest, TResponse>()` - Configure RPC clients
  - `AddRpcHandler<TRequest, TResponse, THandler>()` - Configure RPC handlers
- appsettings.json configuration support with 100% feature parity
- Resilience patterns with Polly integration
  - Automatic retry with exponential backoff and jitter
  - Specific delays per retry attempt
  - Configurable exhaustion actions (DLQ, Requeue, Discard)
  - Connection recovery
- Dead Letter Exchange (DLX) support
  - Automatic DLX configuration
  - Failed message routing
  - Configurable per consumer
- RPC (Request-Reply) pattern support
  - `IRpcClient<TRequest, TResponse>` - Client-side RPC
  - `IRpcHandler<TRequest, TResponse>` - Server-side RPC handlers
  - Direct Reply-To optimization
  - Custom reply queue support
  - Configurable timeouts
- Background consumer hosting via `IHostedService`
  - `AddHostedConsumer<T>()` - Register consumers as hosted services
  - `AddHostedConsumers()` - Register multiple consumers
- QoS (Quality of Service) configuration
  - Prefetch count
  - Prefetch size
  - Global QoS settings
- Comprehensive logging with `ILogger` integration
- Publisher confirms (always enabled)
  - Configurable confirm timeout
- Exchange and queue auto-declaration

### Dependencies
- RabbitMQ.Client 7.0.0
- Polly 8.5.0
- Microsoft.Extensions.Configuration.Abstractions 10.0.0
- Microsoft.Extensions.Configuration.Binder 10.0.0
- Microsoft.Extensions.DependencyInjection.Abstractions 10.0.0
- Microsoft.Extensions.Hosting.Abstractions 10.0.0
- Microsoft.Extensions.Logging.Abstractions 10.0.0
- Microsoft.Extensions.Options 10.0.0

[Unreleased]: https://github.com/jorg3roch4/RabbitX/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/jorg3roch4/RabbitX/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/jorg3roch4/RabbitX/releases/tag/v1.0.0
