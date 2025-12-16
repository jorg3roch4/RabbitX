# Getting Started

This guide will help you set up RabbitX and send your first message in minutes.

## Prerequisites

- **.NET 10.0 SDK** or later
- **RabbitMQ Server** 3.12+ running locally or accessible remotely
- Basic understanding of message queuing concepts

## Installation

Install RabbitX via NuGet:

```bash
dotnet add package RabbitX
```

Or via Package Manager Console:

```powershell
Install-Package RabbitX
```

## RabbitMQ Setup

If you don't have RabbitMQ installed, you can quickly start one using Docker:

```bash
docker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=guest \
  -e RABBITMQ_DEFAULT_PASS=guest \
  rabbitmq:3-management
```

Access the management UI at: http://localhost:15672 (guest/guest)

## Your First Message

Let's create a simple application that publishes and consumes messages.

### Step 1: Define Your Message

```csharp
// Messages/NotificationMessage.cs
public class NotificationMessage
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
```

### Step 2: Create a Message Handler

```csharp
// Handlers/NotificationHandler.cs
using RabbitX.Interfaces;
using RabbitX.Models;

public class NotificationHandler : IMessageHandler<NotificationMessage>
{
    private readonly ILogger<NotificationHandler> _logger;

    public NotificationHandler(ILogger<NotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task<ConsumeResult> HandleAsync(
        MessageContext<NotificationMessage> context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Received notification: {Title} - {Body}",
            context.Message.Title,
            context.Message.Body);

        // Return Ack to acknowledge successful processing
        return Task.FromResult(ConsumeResult.Ack);
    }
}
```

### Step 3: Configure RabbitX

```csharp
// Program.cs
using RabbitX.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitX(options => options
    // Connection settings
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .UseVirtualHost("/")
    .WithClientName("MyFirstApp")

    // Configure a publisher
    .AddPublisher<NotificationMessage>("NotificationPublisher", pub => pub
        .ToExchange("notifications.exchange", "direct")
        .WithRoutingKey("notifications.send")
        .EnablePublisherConfirms())

    // Configure a consumer
    .AddConsumer<NotificationMessage, NotificationHandler>("NotificationConsumer", con => con
        .FromQueue("notifications.queue")
        .BindToExchange("notifications.exchange", "notifications.send")
        .WithPrefetchCount(10)));

var host = builder.Build();
await host.RunAsync();
```

### Step 4: Publish a Message

```csharp
// In a controller or service
public class NotificationService
{
    private readonly IPublisherFactory _publisherFactory;

    public NotificationService(IPublisherFactory publisherFactory)
    {
        _publisherFactory = publisherFactory;
    }

    public async Task SendNotificationAsync(string title, string body)
    {
        var publisher = _publisherFactory.GetPublisher<NotificationMessage>("NotificationPublisher");

        var message = new NotificationMessage
        {
            Title = title,
            Body = body,
            SentAt = DateTime.UtcNow
        };

        var result = await publisher.PublishAsync(message);

        if (result.Success)
        {
            Console.WriteLine($"Message published successfully!");
        }
    }
}
```

## Message Flow

```
┌──────────────┐     ┌─────────────────────┐     ┌───────────────┐
│              │     │                     │     │               │
│  Publisher   │────▶│  notifications      │────▶│  Consumer     │
│              │     │  .exchange          │     │  (Handler)    │
│              │     │                     │     │               │
└──────────────┘     └──────────┬──────────┘     └───────────────┘
                                │                        ▲
                                │ routing key:           │
                                │ notifications.send     │
                                │                        │
                                ▼                        │
                     ┌──────────────────────┐            │
                     │                      │            │
                     │  notifications.queue ├────────────┘
                     │                      │
                     └──────────────────────┘
```

## Complete Minimal Example

Here's a complete console application example:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitX.Extensions;
using RabbitX.Interfaces;
using RabbitX.Models;

// Message
public record OrderCreated(Guid OrderId, string CustomerName, decimal Total);

// Handler
public class OrderCreatedHandler : IMessageHandler<OrderCreated>
{
    public Task<ConsumeResult> HandleAsync(
        MessageContext<OrderCreated> context,
        CancellationToken ct = default)
    {
        Console.WriteLine($"Order received: {context.Message.OrderId} - {context.Message.CustomerName}");
        return Task.FromResult(ConsumeResult.Ack);
    }
}

// Program
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .AddPublisher<OrderCreated>("Orders", p => p
        .ToExchange("shop.orders.exchange", "direct")
        .WithRoutingKey("orders.created"))
    .AddConsumer<OrderCreated, OrderCreatedHandler>("OrdersConsumer", c => c
        .FromQueue("shop.orders.created.queue")
        .BindToExchange("shop.orders.exchange", "orders.created")));

var host = builder.Build();

// Publish a test message after startup
_ = Task.Run(async () =>
{
    await Task.Delay(2000); // Wait for consumer to start

    var factory = host.Services.GetRequiredService<IPublisherFactory>();
    var publisher = factory.GetPublisher<OrderCreated>("Orders");

    await publisher.PublishAsync(new OrderCreated(
        Guid.NewGuid(),
        "John Doe",
        99.99m));

    Console.WriteLine("Message published!");
});

await host.RunAsync();
```

## What's Next?

Now that you have a basic understanding, explore these topics:

- [Configuration](02-configuration.md) - Learn about all configuration options
- [Publishers](03-publishers.md) - Advanced publishing with confirms
- [Consumers](04-consumers.md) - Consumer options and error handling
- [RPC](05-rpc.md) - Request-Reply pattern for synchronous operations

## See Also

- [RabbitMQ Tutorials](https://www.rabbitmq.com/tutorials) - Official RabbitMQ tutorials
- [AMQP Concepts](https://www.rabbitmq.com/tutorials/amqp-concepts) - Understanding exchanges, queues, and bindings
