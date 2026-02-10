# Health Checks

RabbitX provides built-in health check support for ASP.NET Core applications, allowing you to monitor your RabbitMQ connection status through the standard health checks middleware.

## Overview

The health check verifies:

1. **Connection Status** - Fast-fail check on `IsConnected` property
2. **Blocked State** - Detects when broker has blocked the connection (flow control)
3. **Real Communication** - Creates a temporary channel to verify actual broker communication

## Installation

The health check is included in the main RabbitX package. No additional packages required.

```bash
dotnet add package RabbitX
```

## Basic Usage

Register the RabbitX health check after configuring RabbitX:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure RabbitX
builder.Services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("guest", "guest"));

// Add health checks
builder.Services.AddHealthChecks()
    .AddRabbitX();

var app = builder.Build();

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();
```

## Configuration Options

Customize the health check behavior using `RabbitMQHealthCheckOptions`:

```csharp
builder.Services.AddHealthChecks()
    .AddRabbitX(options =>
    {
        options.Name = "rabbitmq-primary";
        options.Tags = new[] { "ready", "messaging" };
        options.Timeout = TimeSpan.FromSeconds(10);
        options.FailureStatus = HealthStatus.Degraded;
    });
```

### Available Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Name` | `string` | `"rabbitmq"` | The name of the health check |
| `Tags` | `IEnumerable<string>?` | `null` | Tags to filter health checks |
| `Timeout` | `TimeSpan` | `5 seconds` | Maximum time for the health check |
| `FailureStatus` | `HealthStatus?` | `null` (Unhealthy) | Status to report on failure |

## Health Status Results

The health check returns different statuses based on the connection state:

| Status | Condition | Description |
|--------|-----------|-------------|
| `Healthy` | Connection open, not blocked, channel created | Everything is working correctly |
| `Degraded` | Connection open but blocked | Broker has applied flow control |
| `Unhealthy` | No connection or channel creation failed | Cannot communicate with broker |

## Response Data

When the health check succeeds, it includes useful data in the response:

```json
{
  "status": "Healthy",
  "results": {
    "rabbitmq": {
      "status": "Healthy",
      "description": "RabbitMQ connection is healthy",
      "data": {
        "host": "localhost",
        "port": 5672,
        "virtualHost": "/",
        "clientName": "RabbitX-MyServer",
        "server": "RabbitMQ",
        "version": "3.12.0",
        "isBlocked": false
      }
    }
  }
}
```

### Data Fields

| Field | Description |
|-------|-------------|
| `host` | RabbitMQ server hostname |
| `port` | RabbitMQ server port |
| `virtualHost` | Connected virtual host |
| `clientName` | Client-provided connection name |
| `server` | RabbitMQ server product name |
| `version` | RabbitMQ server version |
| `isBlocked` | Whether connection is blocked |

## Multiple Health Checks

You can register multiple health checks for different RabbitMQ connections:

```csharp
builder.Services.AddHealthChecks()
    .AddRabbitX(options =>
    {
        options.Name = "rabbitmq-orders";
        options.Tags = new[] { "orders" };
    })
    .AddRabbitX(options =>
    {
        options.Name = "rabbitmq-notifications";
        options.Tags = new[] { "notifications" };
    });
```

## Filtering by Tags

Use tags to create separate health endpoints:

```csharp
// Liveness probe - basic checks only
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // No checks, just confirms app is running
});

// Readiness probe - includes RabbitMQ
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

## Connection Blocked State

RabbitMQ may block connections when the broker is under memory or disk pressure (flow control). The health check detects this through the `IsBlocked` property on `IRabbitMQConnection`:

```csharp
public interface IRabbitMQConnection : IAsyncDisposable
{
    bool IsConnected { get; }
    bool IsBlocked { get; }  // New in 1.1.0
    // ...
}
```

When blocked:
- The health check returns `Degraded` status
- The `isBlocked` field in response data is `true`
- Messages may be delayed but the connection is still usable

## Integration with Kubernetes

Example Kubernetes deployment with health probes:

```yaml
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
        - name: myapp
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 5
```

## Troubleshooting

### Health Check Times Out

Increase the timeout:

```csharp
builder.Services.AddHealthChecks()
    .AddRabbitX(options => options.Timeout = TimeSpan.FromSeconds(30));
```

### Always Returns Unhealthy

1. Verify RabbitMQ is running and accessible
2. Check connection settings (host, port, credentials)
3. Review application logs for connection errors

### Returns Degraded

The broker has applied flow control. Check:
1. RabbitMQ memory usage
2. Disk space on RabbitMQ server
3. Message accumulation in queues

## See Also

- [Getting Started](01-getting-started.md)
- [Configuration](02-configuration.md)
- [Troubleshooting](11-troubleshooting.md)
