# Serialization

RabbitX uses JSON serialization by default but supports custom serializers for different formats or optimization needs.

## Overview

Messages must be serialized to bytes for transmission over RabbitMQ and deserialized when received:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Serialization Flow                                  │
│                                                                              │
│  ┌──────────────┐     ┌─────────────────┐     ┌──────────────┐             │
│  │ C# Object    │────▶│   Serializer    │────▶│ byte[] Data  │             │
│  │ OrderEvent   │     │ (JSON/Binary)   │     │ {...}        │             │
│  └──────────────┘     └─────────────────┘     └──────┬───────┘             │
│                                                      │                      │
│                                                      ▼                      │
│                                               ┌──────────────┐             │
│                                               │  RabbitMQ    │             │
│                                               │  (transport) │             │
│                                               └──────┬───────┘             │
│                                                      │                      │
│                                                      ▼                      │
│  ┌──────────────┐     ┌─────────────────┐     ┌──────────────┐             │
│  │ C# Object    │◀────│  Deserializer   │◀────│ byte[] Data  │             │
│  │ OrderEvent   │     │                 │     │ {...}        │             │
│  └──────────────┘     └─────────────────┘     └──────────────┘             │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## IMessageSerializer Interface

```csharp
namespace RabbitX.Serialization;

public interface IMessageSerializer
{
    /// <summary>
    /// Serializes an object to a byte array.
    /// </summary>
    byte[] Serialize<T>(T message) where T : class;

    /// <summary>
    /// Deserializes a byte array to a strongly-typed object.
    /// </summary>
    T? Deserialize<T>(ReadOnlyMemory<byte> data) where T : class;

    /// <summary>
    /// Deserializes a byte array to an object of the specified type.
    /// </summary>
    object? Deserialize(ReadOnlyMemory<byte> data, Type type);

    /// <summary>
    /// Gets the content type for HTTP headers (e.g., "application/json").
    /// </summary>
    string ContentType { get; }
}
```

## Default JSON Serializer

RabbitX uses `System.Text.Json` by default with sensible settings:

```csharp
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonMessageSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public string ContentType => "application/json";

    public byte[] Serialize<T>(T message) where T : class
        => JsonSerializer.SerializeToUtf8Bytes(message, _options);

    public T? Deserialize<T>(ReadOnlyMemory<byte> data) where T : class
        => JsonSerializer.Deserialize<T>(data.Span, _options);

    public object? Deserialize(ReadOnlyMemory<byte> data, Type type)
        => JsonSerializer.Deserialize(data.Span, type, _options);
}
```

### Default Serialization Behavior

| Setting | Value | Effect |
|---------|-------|--------|
| `PropertyNamingPolicy` | `CamelCase` | `OrderId` → `"orderId"` |
| `PropertyNameCaseInsensitive` | `true` | Accepts `"orderId"` or `"OrderId"` |
| `WriteIndented` | `false` | Compact JSON for smaller messages |
| `DefaultIgnoreCondition` | `WhenWritingNull` | Omits null properties |

## Configuring the Serializer

### Using Default JSON Serializer

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .UseJsonSerialization());  // Default, can be omitted
```

### Custom JSON Options

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .UseJsonSerialization(json =>
    {
        json.PropertyNamingPolicy = null;  // Use PascalCase
        json.WriteIndented = true;         // Pretty print (debugging)
        json.Converters.Add(new JsonStringEnumConverter());
    }));
```

### Using a Custom Serializer

```csharp
services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest")
    .UseSerializer<MessagePackSerializer>());
```

## Creating a Custom Serializer

### Example: MessagePack Serializer

[MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) is a fast binary serialization format:

```csharp
using MessagePack;
using RabbitX.Serialization;

public sealed class MessagePackSerializer : IMessageSerializer
{
    private readonly MessagePackSerializerOptions _options;

    public MessagePackSerializer()
    {
        _options = MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray);
    }

    public string ContentType => "application/x-msgpack";

    public byte[] Serialize<T>(T message) where T : class
    {
        return MessagePackSerializer.Serialize(message, _options);
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> data) where T : class
    {
        return MessagePackSerializer.Deserialize<T>(data, _options);
    }

    public object? Deserialize(ReadOnlyMemory<byte> data, Type type)
    {
        return MessagePackSerializer.Deserialize(type, data, _options);
    }
}
```

**Message class for MessagePack:**

```csharp
[MessagePackObject]
public class OrderEvent
{
    [Key(0)]
    public Guid OrderId { get; set; }

    [Key(1)]
    public string CustomerId { get; set; } = string.Empty;

    [Key(2)]
    public decimal Total { get; set; }

    [Key(3)]
    public DateTime CreatedAt { get; set; }
}
```

### Example: Protobuf Serializer

[Protocol Buffers](https://github.com/protobuf-net/protobuf-net) for schema-based serialization:

```csharp
using ProtoBuf;
using RabbitX.Serialization;

public sealed class ProtobufSerializer : IMessageSerializer
{
    public string ContentType => "application/x-protobuf";

    public byte[] Serialize<T>(T message) where T : class
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        return stream.ToArray();
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> data) where T : class
    {
        using var stream = new MemoryStream(data.ToArray());
        return Serializer.Deserialize<T>(stream);
    }

    public object? Deserialize(ReadOnlyMemory<byte> data, Type type)
    {
        using var stream = new MemoryStream(data.ToArray());
        return Serializer.NonGeneric.Deserialize(type, stream);
    }
}
```

**Message class for Protobuf:**

```csharp
[ProtoContract]
public class OrderEvent
{
    [ProtoMember(1)]
    public Guid OrderId { get; set; }

    [ProtoMember(2)]
    public string CustomerId { get; set; } = string.Empty;

    [ProtoMember(3)]
    public decimal Total { get; set; }

    [ProtoMember(4)]
    public DateTime CreatedAt { get; set; }
}
```

## Serialization Best Practices

### 1. Keep Messages Small

```csharp
// Good: Only essential data
public record OrderCreated(Guid OrderId, string CustomerId, decimal Total);

// Avoid: Large embedded objects
public record OrderCreated(
    Guid OrderId,
    Customer Customer,        // Full customer object
    List<OrderLine> Lines,    // Full line items
    byte[] Receipt);          // Binary data
```

### 2. Use Appropriate Types

```csharp
public class OrderEvent
{
    // Good: Guid serializes efficiently
    public Guid OrderId { get; set; }

    // Good: Enum as string for readability
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrderStatus Status { get; set; }

    // Good: DateTimeOffset for timezone awareness
    public DateTimeOffset CreatedAt { get; set; }

    // Avoid: DateTime without timezone info
    // public DateTime CreatedAt { get; set; }
}
```

### 3. Version Your Messages

```csharp
public class OrderEventV1
{
    public int Version => 1;
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }  // Renamed in V2
}

public class OrderEventV2
{
    public int Version => 2;
    public Guid OrderId { get; set; }
    public decimal Total { get; set; }   // Was "Amount" in V1
    public string Currency { get; set; } // New field
}
```

### 4. Handle Nulls Gracefully

```csharp
public class NotificationEvent
{
    public required string Title { get; set; }

    // Nullable with default
    public string? Description { get; set; }

    // Collection should never be null
    public List<string> Tags { get; set; } = new();
}
```

## Serialization Comparison

| Format | Size | Speed | Schema | Human Readable |
|--------|------|-------|--------|----------------|
| JSON | Large | Medium | No | Yes |
| MessagePack | Small | Fast | No | No |
| Protobuf | Smallest | Fastest | Yes | No |

### When to Use Each

**JSON (Default):**
- Debugging and development
- Interoperability with other systems
- Human-readable logs
- Most applications

**MessagePack:**
- High throughput requirements
- Large message volumes
- When size matters but schema evolution is needed

**Protobuf:**
- Maximum performance
- Strict schema enforcement
- Cross-language compatibility
- Large-scale distributed systems

## Troubleshooting Serialization

### Common Issues

**1. Property not serialized:**

```csharp
// Problem: Private setter
public class Event
{
    public string Value { get; private set; }  // Won't deserialize
}

// Solution: Use init or public setter
public class Event
{
    public string Value { get; init; }  // Works with init
}
```

**2. Enum serialization:**

```csharp
// Problem: Enum serializes as number
public OrderStatus Status { get; set; }  // Outputs: 1

// Solution: Add converter
[JsonConverter(typeof(JsonStringEnumConverter))]
public OrderStatus Status { get; set; }  // Outputs: "Pending"
```

**3. DateTime timezone issues:**

```csharp
// Problem: DateTime loses timezone
public DateTime CreatedAt { get; set; }  // "2024-01-15T10:30:00"

// Solution: Use DateTimeOffset
public DateTimeOffset CreatedAt { get; set; }  // "2024-01-15T10:30:00+00:00"
```

**4. Circular references:**

```csharp
// Problem: Circular reference
public class Order
{
    public Customer Customer { get; set; }
}
public class Customer
{
    public List<Order> Orders { get; set; }  // Circular!
}

// Solution: Use [JsonIgnore] or reference handling
public class Customer
{
    [JsonIgnore]
    public List<Order> Orders { get; set; }
}
```

## Content Type Header

RabbitX sets the `content_type` property on messages:

```csharp
// When publishing
properties.ContentType = _serializer.ContentType;  // "application/json"
```

Consumers can check this to handle multiple formats:

```csharp
public async Task<ConsumeResult> HandleAsync(
    MessageContext<OrderEvent> context,
    CancellationToken ct)
{
    // Content type is available in headers if needed
    _logger.LogDebug("Content-Type: {ContentType}",
        context.Headers.TryGetValue("content_type", out var ct) ? ct : "unknown");

    // Message is already deserialized
    var order = context.Message;

    return ConsumeResult.Ack;
}
```

## See Also

- [Configuration](02-configuration.md) - Serializer configuration options
- [System.Text.Json Documentation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview) - JSON serialization
- [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp) - Binary serialization
- [protobuf-net](https://github.com/protobuf-net/protobuf-net) - Protocol Buffers for .NET
