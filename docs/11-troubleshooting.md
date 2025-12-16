# Troubleshooting

This guide covers common issues and their solutions when working with RabbitX.

## Connection Issues

### Cannot Connect to RabbitMQ

**Symptoms:**
- `RabbitMQ.Client.Exceptions.BrokerUnreachableException`
- "Connection refused" errors
- Application hangs on startup

**Solutions:**

1. **Verify RabbitMQ is running:**
   ```bash
   # Check if RabbitMQ is listening
   curl -s http://localhost:15672 | head -1

   # Docker: Check container status
   docker ps | grep rabbitmq
   ```

2. **Check connection settings:**
   ```csharp
   services.AddRabbitX(options => options
       .UseConnection("localhost", 5672)      // Verify host and port
       .UseCredentials("guest", "guest")      // Verify credentials
       .UseVirtualHost("/"));                 // Verify virtual host
   ```

3. **Check firewall/network:**
   ```bash
   # Test port connectivity
   telnet localhost 5672

   # Or with netcat
   nc -zv localhost 5672
   ```

4. **Verify credentials in RabbitMQ:**
   ```bash
   # List users
   rabbitmqctl list_users

   # Check user permissions
   rabbitmqctl list_permissions
   ```

### Connection Drops Randomly

**Symptoms:**
- Intermittent `AlreadyClosedException`
- Messages not being delivered
- "Channel is closed" errors

**Solutions:**

1. **Check heartbeat settings:**
   ```csharp
   .UseConnection("localhost", 5672)
   .WithHeartbeat(TimeSpan.FromSeconds(60))  // Adjust as needed
   ```

2. **Monitor connection with management UI:**
   - Open `http://localhost:15672`
   - Check Connections tab for issues
   - Look for blocked or closing connections

3. **Check for memory alarms:**
   ```bash
   rabbitmqctl status | grep memory
   ```

4. **Review RabbitMQ logs:**
   ```bash
   # Docker
   docker logs rabbitmq

   # Native install (location varies)
   tail -f /var/log/rabbitmq/rabbit@hostname.log
   ```

## Publisher Issues

### Messages Not Being Published

**Symptoms:**
- `PublishResult.Success` is `false`
- Queue shows 0 messages
- No errors but messages don't arrive

**Solutions:**

1. **Check exchange and routing key:**
   ```csharp
   // Ensure exchange exists and routing key matches bindings
   .AddPublisher<MyEvent>("Publisher", pub => pub
       .ToExchange("my.exchange", "topic")        // Exchange must exist
       .WithRoutingKey("my.routing.key"))         // Must match consumer binding
   ```

2. **Enable publisher confirms:**
   ```csharp
   .AddPublisher<MyEvent>("Publisher", pub => pub
       .ToExchange("my.exchange", "topic")
       .WithRoutingKey("my.routing.key")
       .EnablePublisherConfirms())  // Get confirmation from broker
   ```

3. **Check the result:**
   ```csharp
   var result = await publisher.PublishAsync(message);

   if (!result.Success)
   {
       _logger.LogError(
           "Publish failed: {Status} - {Error}",
           result.Status,
           result.ErrorMessage);
   }
   ```

4. **Verify exchange bindings in management UI:**
   - Go to `http://localhost:15672/#/exchanges`
   - Click on your exchange
   - Check "Bindings" section

### Publisher Confirms Timeout

**Symptoms:**
- `PublishStatus.ConfirmTimeout`
- Long delays when publishing
- Messages may or may not be delivered

**Solutions:**

1. **Increase confirm timeout:**
   ```csharp
   .AddPublisher<MyEvent>("Publisher", pub => pub
       .ToExchange("my.exchange", "topic")
       .EnablePublisherConfirms()
       .WithConfirmTimeout(TimeSpan.FromSeconds(30)))  // Increase from default
   ```

2. **Check RabbitMQ disk/memory pressure:**
   ```bash
   rabbitmqctl status | grep -E "(disk|memory)"
   ```

3. **Consider async publishing without confirms for non-critical messages:**
   ```csharp
   // Skip confirms for high-throughput, non-critical messages
   .AddPublisher<MetricsEvent>("Metrics", pub => pub
       .ToExchange("metrics.exchange", "fanout"))
       // No EnablePublisherConfirms()
   ```

## Consumer Issues

### Messages Not Being Consumed

**Symptoms:**
- Messages accumulating in queue
- Handler never called
- No errors in logs

**Solutions:**

1. **Verify consumer is registered:**
   ```csharp
   services.AddRabbitX(options => options
       .UseConnection("localhost", 5672)
       .AddConsumer<MyEvent, MyHandler>("MyConsumer", con => con
           .FromQueue("my.queue")
           .BindToExchange("my.exchange", "my.routing.key")));

   // Handler must be registered
   services.AddScoped<MyHandler>();
   ```

2. **Check hosted service is starting:**
   ```csharp
   // In Program.cs, ensure app runs
   await host.RunAsync();  // Not just host.Build()
   ```

3. **Verify queue bindings:**
   - Check `http://localhost:15672/#/queues`
   - Click your queue
   - Verify "Bindings" section shows correct exchange and routing key

4. **Check consumer prefetch:**
   ```csharp
   .AddConsumer<MyEvent, MyHandler>("Consumer", con => con
       .FromQueue("my.queue")
       .WithPrefetchCount(10))  // Ensure not 0
   ```

### Handler Throws Exception but Message Requeues Forever

**Symptoms:**
- Same message processed repeatedly
- Error logs showing same MessageId
- CPU usage spikes

**Solutions:**

1. **Return appropriate ConsumeResult:**
   ```csharp
   public async Task<ConsumeResult> HandleAsync(
       MessageContext<MyEvent> context,
       CancellationToken ct)
   {
       try
       {
           await ProcessAsync(context.Message);
           return ConsumeResult.Ack;
       }
       catch (ValidationException)
       {
           // Bad message - don't retry
           return ConsumeResult.Reject;  // Goes to DLQ
       }
       catch (TransientException) when (GetRetryCount(context) < 3)
       {
           // Transient error - retry
           return ConsumeResult.Nack;  // Requeue
       }
       catch (Exception)
       {
           // Too many retries - reject
           return ConsumeResult.Reject;  // Goes to DLQ
       }
   }
   ```

2. **Configure dead letter queue:**
   ```csharp
   .AddConsumer<MyEvent, MyHandler>("Consumer", con => con
       .FromQueue("my.queue")
       .WithDeadLetter(dlq => dlq
           .ToExchange("my.dlx.exchange")
           .ToQueue("my.dead-letters.queue")))
   ```

3. **Track retry count:**
   ```csharp
   private int GetRetryCount(MessageContext<MyEvent> context)
   {
       if (context.Headers.TryGetValue("x-death", out var deaths))
       {
           return ((List<object>)deaths).Count;
       }
       return 0;
   }
   ```

### Consumer Stops Processing

**Symptoms:**
- Consumer was working, then stops
- Queue messages accumulate
- No errors in application logs

**Solutions:**

1. **Check for unacknowledged messages:**
   - Go to `http://localhost:15672/#/queues`
   - Look at "Unacked" column
   - High unacked count indicates stuck handler

2. **Add handler timeout:**
   ```csharp
   public async Task<ConsumeResult> HandleAsync(
       MessageContext<MyEvent> context,
       CancellationToken ct)
   {
       using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
       cts.CancelAfter(TimeSpan.FromMinutes(5));  // Handler timeout

       try
       {
           await ProcessAsync(context.Message, cts.Token);
           return ConsumeResult.Ack;
       }
       catch (OperationCanceledException)
       {
           _logger.LogWarning("Handler timed out");
           return ConsumeResult.Nack;
       }
   }
   ```

3. **Check for blocking calls:**
   ```csharp
   // Bad: Blocks thread
   var result = SomeAsyncMethod().Result;

   // Good: Async all the way
   var result = await SomeAsyncMethod();
   ```

## RPC Issues

### RPC Timeout

**Symptoms:**
- `RpcResult.IsTimeout` is `true`
- Client hangs then fails
- Handler never receives request

**Solutions:**

1. **Verify handler is running:**
   ```csharp
   // Handler must be registered and started
   .AddRpcHandler<MyRequest, MyResponse, MyHandler>("Handler", h => h
       .FromQueue("my.rpc.queue")
       .BindToExchange("my.rpc.exchange", "my.rpc.key"))
   ```

2. **Check exchange binding matches:**
   ```csharp
   // Client
   .AddRpcClient<MyRequest, MyResponse>("Client", rpc => rpc
       .ToExchange("my.rpc.exchange", "direct")
       .WithRoutingKey("my.rpc.key"))  // Must match

   // Handler
   .AddRpcHandler<MyRequest, MyResponse, MyHandler>("Handler", h => h
       .FromQueue("my.rpc.queue")
       .BindToExchange("my.rpc.exchange", "my.rpc.key"))  // Must match
   ```

3. **Increase timeout:**
   ```csharp
   .AddRpcClient<MyRequest, MyResponse>("Client", rpc => rpc
       .ToExchange("my.rpc.exchange", "direct")
       .WithRoutingKey("my.rpc.key")
       .WithTimeout(TimeSpan.FromSeconds(60)))  // Increase
   ```

4. **Use TryCallAsync for better error handling:**
   ```csharp
   var result = await client.TryCallAsync(request);

   if (result.IsTimeout)
   {
       _logger.LogWarning("RPC timed out after {Duration}", result.Duration);
   }
   else if (!result.Success)
   {
       _logger.LogError("RPC failed: {Error}", result.ErrorMessage);
   }
   ```

### RPC Handler Not Receiving Messages

**Symptoms:**
- Client times out
- Handler logs show no activity
- Queue exists but stays empty

**Solutions:**

1. **Ensure exchange is declared:**
   ```csharp
   // Handler binds to exchange - exchange must exist
   // RabbitX declares it automatically if DeclareExchange = true
   .AddRpcHandler<MyRequest, MyResponse, MyHandler>("Handler", h => h
       .FromQueue("my.rpc.queue")
       .BindToExchange("my.rpc.exchange", "my.rpc.key")
       .DeclareExchange())  // Ensure exchange is created
   ```

2. **Check exchange type matches:**
   ```csharp
   // Both must use same exchange type
   .AddRpcClient<MyRequest, MyResponse>("Client", rpc => rpc
       .ToExchange("my.rpc.exchange", "direct"))  // "direct"

   .AddRpcHandler<MyRequest, MyResponse, MyHandler>("Handler", h => h
       .FromQueue("my.rpc.queue")
       .BindToExchange("my.rpc.exchange", "my.rpc.key")
       .WithExchangeType("direct"))  // Must match: "direct"
   ```

## Serialization Issues

### Deserialization Fails

**Symptoms:**
- `JsonException` in consumer
- Message rejected immediately
- Works with some messages, fails with others

**Solutions:**

1. **Check property naming:**
   ```csharp
   // Publisher sends: {"orderId": "123"}
   // Consumer expects: {"OrderId": "123"}

   // Solution: Ensure case-insensitive
   services.AddRabbitX(options => options
       .UseJsonSerialization(json =>
       {
           json.PropertyNameCaseInsensitive = true;
       }));
   ```

2. **Handle null values:**
   ```csharp
   public class MyEvent
   {
       // Use nullable types for optional fields
       public string? OptionalField { get; set; }

       // Use default values for collections
       public List<string> Tags { get; set; } = new();
   }
   ```

3. **Check for type mismatches:**
   ```csharp
   // Bad: int vs string mismatch
   // Publisher: { "count": 123 }
   // Consumer: public string Count { get; set; }

   // Good: Types match
   public int Count { get; set; }
   ```

4. **Add enum converter:**
   ```csharp
   services.AddRabbitX(options => options
       .UseJsonSerialization(json =>
       {
           json.Converters.Add(new JsonStringEnumConverter());
       }));
   ```

## Performance Issues

### Slow Message Processing

**Symptoms:**
- Messages accumulate faster than processed
- High latency
- Queue depth keeps growing

**Solutions:**

1. **Increase prefetch count:**
   ```csharp
   .AddConsumer<MyEvent, MyHandler>("Consumer", con => con
       .FromQueue("my.queue")
       .WithPrefetchCount(50))  // Process more in parallel
   ```

2. **Scale consumers:**
   ```bash
   # Run multiple instances
   dotnet run &
   dotnet run &
   dotnet run &
   ```

3. **Optimize handler:**
   ```csharp
   public async Task<ConsumeResult> HandleAsync(
       MessageContext<MyEvent> context,
       CancellationToken ct)
   {
       // Don't: Heavy processing inline
       // await HeavyOperation(context.Message);

       // Do: Queue for background processing
       await _backgroundQueue.EnqueueAsync(context.Message);
       return ConsumeResult.Ack;
   }
   ```

4. **Use appropriate exchange type:**
   ```csharp
   // Fanout is faster than topic for broadcasting
   .ToExchange("broadcast.exchange", "fanout")

   // Direct is faster than topic for exact matching
   .ToExchange("orders.exchange", "direct")
   ```

### High Memory Usage

**Symptoms:**
- Application memory grows over time
- OutOfMemoryException
- GC pressure warnings

**Solutions:**

1. **Limit prefetch:**
   ```csharp
   .WithPrefetchCount(10)  // Don't buffer too many messages
   ```

2. **Process and dispose properly:**
   ```csharp
   public async Task<ConsumeResult> HandleAsync(
       MessageContext<MyEvent> context,
       CancellationToken ct)
   {
       using var scope = _scopeFactory.CreateScope();
       var service = scope.ServiceProvider.GetRequiredService<IMyService>();

       await service.ProcessAsync(context.Message);

       return ConsumeResult.Ack;
   }  // Scope disposed, resources freed
   ```

3. **Don't store message references:**
   ```csharp
   // Bad: Storing messages
   private List<MyEvent> _allMessages = new();

   public async Task<ConsumeResult> HandleAsync(...)
   {
       _allMessages.Add(context.Message);  // Memory leak!
   }
   ```

## Logging and Debugging

### Enable Detailed Logging

```csharp
// In appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "RabbitX": "Debug",
      "RabbitMQ.Client": "Warning"
    }
  }
}
```

### Trace Message Flow

```csharp
// Add correlation ID to all messages
var correlationId = Guid.NewGuid().ToString();

await publisher.PublishAsync(message, new PublishOptions
{
    CorrelationId = correlationId,
    Headers = new Dictionary<string, object>
    {
        ["x-trace-id"] = Activity.Current?.TraceId.ToString() ?? correlationId
    }
});

// In consumer
public async Task<ConsumeResult> HandleAsync(
    MessageContext<MyEvent> context,
    CancellationToken ct)
{
    using var _ = _logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = context.CorrelationId ?? "unknown",
        ["MessageId"] = context.MessageId
    });

    _logger.LogInformation("Processing message");
    // ...
}
```

### Use RabbitMQ Management UI

Key pages to check:

- **Overview**: `http://localhost:15672/#/`
  - Connection count
  - Message rates
  - Node health

- **Queues**: `http://localhost:15672/#/queues`
  - Message count
  - Consumer count
  - Message rates per queue

- **Exchanges**: `http://localhost:15672/#/exchanges`
  - Bindings
  - Message rates

- **Connections**: `http://localhost:15672/#/connections`
  - Client connections
  - Channel count
  - Blocked status

## Getting Help

If you're still stuck:

1. **Check RabbitMQ logs:**
   ```bash
   docker logs rabbitmq --tail 100
   ```

2. **Enable RabbitMQ tracing:**
   ```bash
   rabbitmqctl trace_on
   # Check traces in management UI
   ```

3. **Review official documentation:**
   - [RabbitMQ Documentation](https://www.rabbitmq.com/docs)
   - [RabbitMQ Troubleshooting](https://www.rabbitmq.com/docs/troubleshooting)
   - [RabbitMQ .NET Client](https://www.rabbitmq.com/docs/dotnet-api-guide)

4. **Check GitHub issues:**
   - Search existing issues for similar problems
   - Open a new issue with reproduction steps

## See Also

- [Configuration](02-configuration.md) - Setup options
- [Dead Letter Queues](07-dead-letter-queues.md) - Handling failed messages
- [RabbitMQ Management](https://www.rabbitmq.com/docs/management) - Management UI guide
