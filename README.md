![RabbitX Logo](https://raw.githubusercontent.com/jorg3roch4/RabbitX/main/assets/rabbitx-brand.png)

# RabbitX

**The Modern RabbitMQ Integration for .NET 10+**

[![NuGet](https://img.shields.io/nuget/v/RabbitX.svg?style=flat-square)](https://www.nuget.org/packages/RabbitX)[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](https://github.com/jorg3roch4/RabbitX/blob/main/LICENSE)[![C#](https://img.shields.io/badge/C%23-14-239120.svg?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/)[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat-square)](https://dotnet.microsoft.com/)

**RabbitX** is a robust RabbitMQ abstraction library designed exclusively for modern .NET applications. Built on top of the official **RabbitMQ.Client 7.x** and **Polly 8.x**, it provides a clean fluent API for publishers and consumers, built-in resilience patterns, retry policies with exponential backoff, dead letter exchange support, and RPC capabilities.

Version 1.x marks the initial release: a complete RabbitMQ integration solution with 100% feature parity between Fluent API and appsettings.json configuration, comprehensive retry strategies, and automatic dead letter exchange handling.

Our philosophy is simple: make RabbitMQ integration as straightforward as possible while providing enterprise-grade reliability. RabbitX is built with the latest **C# 14** features and targets **.NET 10**. This is not just another library; it's a commitment to simplifying message-driven architectures.

---

## 💖 Support the Project

RabbitX is a passion project, driven by the desire to simplify RabbitMQ integration for the .NET community. Maintaining this library requires significant effort: staying current with each .NET release, addressing issues promptly, implementing new features, and keeping documentation up to date.

If RabbitX has helped you build better applications or saved you development time, I would be incredibly grateful for your support. Your contribution—no matter the size—helps me dedicate time to respond to issues quickly, implement improvements, and keep the library evolving alongside the .NET platform.

**I'm also looking for sponsors** who believe in this project's mission. Sponsorship helps ensure RabbitX remains actively maintained and continues to serve the .NET community for years to come.

Of course, there's absolutely no obligation. If you prefer, simply starring the repository or sharing RabbitX with fellow developers is equally appreciated!

- ⭐ **Star the repository** on GitHub to raise its visibility
- 💬 **Share** RabbitX with your team or community
- ☕ **Support via Donations:**

  - [![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white)](https://paypal.me/jorg3roch4)
  - [![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/jorg3roch4)

---

## ✨ Features

- **Fluent API**: Type-safe configuration with full IntelliSense support
- **appsettings.json**: Complete configuration from JSON files with 100% parity
- **Multiple Publishers & Consumers**: Configure and use multiple named publishers and consumers
- **Reliable Publishing**: Publisher confirmations with broker acknowledgments
- **Retry Policies**: Specific delays or exponential backoff with jitter support
- **Dead Letter Exchange**: Automatic DLX setup for failed message routing
- **RPC Support**: Request-Reply pattern with Direct Reply-To optimization
- **QoS Control**: Prefetch count, prefetch size, and global QoS settings
- **Connection Recovery**: Automatic reconnection on connection failures
- **Polly Integration**: Built-in resilience with the Polly library
- **Health Checks**: Built-in health check for ASP.NET Core with connection and blocked state detection

---

## 🎉 What's New in 1.1.0

**Health Checks!** RabbitX 1.1.0 adds built-in health check support for ASP.NET Core:

- **Connection Status**: Fast-fail check verifying `IsConnected` state
- **Blocked Detection**: Reports `Degraded` status when connection is blocked by broker
- **Real Communication**: Creates temporary channel to verify actual broker communication
- **Rich Data**: Includes server info, version, virtual host, and client name in health response
- **Configurable**: Custom name, tags, timeout, and failure status options

```csharp
// Basic usage
services.AddHealthChecks().AddRabbitX();

// With configuration
services.AddHealthChecks().AddRabbitX(options =>
{
    options.Name = "rabbitmq-primary";
    options.Tags = new[] { "ready", "messaging" };
    options.Timeout = TimeSpan.FromSeconds(10);
});
```

See [CHANGELOG.md](CHANGELOG.md) for full details.

---

## 🚀 Getting Started

### Installation

```bash
dotnet add package RabbitX
```

### Configuration

Register RabbitX in your `Program.cs` with either Fluent API or appsettings.json.

#### Option A: Fluent API

```csharp
builder.Services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddPublisher("OrderPublisher", pub => pub
        .ToExchange("shop.orders.exchange", "topic")
        .WithRoutingKey("orders.created"))
    .AddConsumer("OrderConsumer", con => con
        .FromQueue("shop.orders.created.queue")
        .BindToExchange("shop.orders.exchange", "orders.created")));
```

#### Option B: appsettings.json

```json
{
  "RabbitX": {
    "Connection": { "HostName": "localhost", "UserName": "guest", "Password": "guest" },
    "Publishers": {
      "OrderPublisher": { "Exchange": "shop.orders.exchange", "ExchangeType": "topic", "RoutingKey": "orders.created" }
    },
    "Consumers": {
      "OrderConsumer": { "Queue": "shop.orders.created.queue", "Exchange": "shop.orders.exchange", "RoutingKey": "orders.created" }
    }
  }
}
```

```csharp
builder.Services.AddRabbitX(builder.Configuration);
```

### Publishing Messages

Inject `IPublisherFactory` and create a publisher to send messages:

```csharp
public record OrderCreated(Guid OrderId, string CustomerId, decimal Total);

public class OrderService(IPublisherFactory factory)
{
    public async Task CreateOrderAsync(Order order)
    {
        var publisher = factory.CreateReliablePublisher<OrderCreated>("OrderPublisher");
        var message = new OrderCreated(order.Id, order.CustomerId, order.Total);
        await publisher.PublishWithConfirmAsync(message);
    }
}
```

### Consuming Messages

Create a handler to process incoming messages:

```csharp
public class OrderCreatedHandler : IMessageHandler<OrderCreated>
{
    public Task<ConsumeResult> HandleAsync(MessageContext<OrderCreated> context, CancellationToken ct)
    {
        var order = context.Message;
        Console.WriteLine($"Processing order {order.OrderId} for {order.CustomerId}");
        // Process the order...
        return Task.FromResult(ConsumeResult.Ack);
    }
}
```

Register the handler and start the consumer as a hosted service:

```csharp
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
builder.Services.AddHostedConsumer<OrderCreated>("OrderConsumer");
```

---

## 📅 Versioning & .NET Support Policy

RabbitX follows a clear versioning strategy aligned with .NET's release cadence:

### Version History

| RabbitX | .NET | C# | Status |
|---------|------|-----|--------|
| **1.x** | **.NET 10** | **C# 14** | **Current** |

### Future Support Policy

RabbitX will always support the **current LTS version** plus the **next standard release**. When a new LTS version is released, support for older versions will be discontinued:

| RabbitX | .NET | C# | Notes |
|---------|------|-----|-------|
| 1.x | .NET 10 | C# 14 | LTS only |
| 2.x | .NET 10 + .NET 11 | C# 14 / C# 15 | LTS + Standard |
| 3.x | .NET 12 | C# 16 | New LTS (drops .NET 10/11) |

**Why this policy?**
- **Focused development:** By limiting supported versions, we can dedicate more effort to quality, performance, and new features
- **Modern features:** Each .NET version brings improvements that RabbitX can fully leverage
- **Clear upgrade path:** Users know exactly when to plan their upgrades

> **Note:** We recommend always using the latest LTS version of .NET for production applications.

---

## 📚 Documentation

Comprehensive guides to help you master RabbitX:

### Getting Started
- **[Getting Started](https://github.com/jorg3roch4/RabbitX/blob/main/docs/01-getting-started.md)** - Installation, requirements, and first message
- **[Configuration](https://github.com/jorg3roch4/RabbitX/blob/main/docs/02-configuration.md)** - Fluent API and appsettings.json complete reference

### Core Features
- **[Publishers](https://github.com/jorg3roch4/RabbitX/blob/main/docs/03-publishers.md)** - Publishing messages with confirms
- **[Consumers](https://github.com/jorg3roch4/RabbitX/blob/main/docs/04-consumers.md)** - Consuming messages with handlers
- **[RPC](https://github.com/jorg3roch4/RabbitX/blob/main/docs/05-rpc.md)** - Request-Reply pattern

### Advanced Topics
- **[Retry & Resilience](https://github.com/jorg3roch4/RabbitX/blob/main/docs/06-retry-resilience.md)** - Retry policies and strategies
- **[Dead Letter Queues](https://github.com/jorg3roch4/RabbitX/blob/main/docs/07-dead-letter-queues.md)** - DLX configuration
- **[Health Checks](https://github.com/jorg3roch4/RabbitX/blob/main/docs/12-health-checks.md)** - ASP.NET Core health check integration

### Examples
Check out the **[samples folder](https://github.com/jorg3roch4/RabbitX/tree/main/samples)** for complete working examples.

---

## 📋 Requirements

- **.NET 10.0** or later
- **RabbitMQ 3.12+** (recommended)
- **RabbitMQ.Client 7.0.0** (included as dependency)
- **Polly 8.5.0** (included as dependency)

---

## 🙏 Acknowledgments

RabbitX is built on top of excellent open-source libraries:

- **[RabbitMQ.Client](https://github.com/rabbitmq/rabbitmq-dotnet-client)** - The official RabbitMQ .NET client by VMware
- **[Polly](https://github.com/App-vNext/Polly)** - The resilience and transient-fault-handling library

We are immensely grateful for their contribution to the .NET ecosystem, which provided the foundation for this library.
