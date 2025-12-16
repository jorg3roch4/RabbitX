using Microsoft.Extensions.Configuration;
using RabbitX.Configuration;
using RabbitX.Rpc;

namespace RabbitX.Builders;

/// <summary>
/// Fluent builder for configuring RabbitX options.
/// Supports both programmatic configuration and loading from IConfiguration.
/// </summary>
public sealed class RabbitXOptionsBuilder
{
    private readonly RabbitXOptions _options = new();

    /// <summary>
    /// Loads configuration from an IConfiguration section.
    /// Can be combined with fluent methods to override specific values.
    /// </summary>
    /// <param name="configuration">The configuration section containing RabbitX settings.</param>
    /// <returns>The builder for chaining.</returns>
    public RabbitXOptionsBuilder LoadFromConfiguration(IConfigurationSection configuration)
    {
        configuration.Bind(_options);
        return this;
    }

    #region Connection Configuration

    /// <summary>
    /// Configures the RabbitMQ connection host and port.
    /// </summary>
    public RabbitXOptionsBuilder UseConnection(string hostName, int port = 5672)
    {
        _options.Connection.HostName = hostName;
        _options.Connection.Port = port;
        return this;
    }

    /// <summary>
    /// Configures the RabbitMQ credentials.
    /// </summary>
    public RabbitXOptionsBuilder UseCredentials(string userName, string password)
    {
        _options.Connection.UserName = userName;
        _options.Connection.Password = password;
        return this;
    }

    /// <summary>
    /// Configures the virtual host.
    /// </summary>
    public RabbitXOptionsBuilder UseVirtualHost(string virtualHost)
    {
        _options.Connection.VirtualHost = virtualHost;
        return this;
    }

    /// <summary>
    /// Enables automatic connection recovery.
    /// </summary>
    /// <param name="recoveryInterval">Interval between recovery attempts.</param>
    public RabbitXOptionsBuilder EnableAutoRecovery(TimeSpan? recoveryInterval = null)
    {
        _options.Connection.AutomaticRecoveryEnabled = true;
        if (recoveryInterval.HasValue)
        {
            _options.Connection.NetworkRecoveryIntervalSeconds = (int)recoveryInterval.Value.TotalSeconds;
        }
        return this;
    }

    /// <summary>
    /// Disables automatic connection recovery.
    /// </summary>
    public RabbitXOptionsBuilder DisableAutoRecovery()
    {
        _options.Connection.AutomaticRecoveryEnabled = false;
        return this;
    }

    /// <summary>
    /// Sets the connection timeout.
    /// </summary>
    public RabbitXOptionsBuilder WithConnectionTimeout(TimeSpan timeout)
    {
        _options.Connection.ConnectionTimeoutSeconds = (int)timeout.TotalSeconds;
        return this;
    }

    /// <summary>
    /// Sets the heartbeat interval.
    /// </summary>
    public RabbitXOptionsBuilder WithHeartbeat(TimeSpan interval)
    {
        _options.Connection.RequestedHeartbeatSeconds = (int)interval.TotalSeconds;
        return this;
    }

    /// <summary>
    /// Sets the client-provided connection name.
    /// </summary>
    public RabbitXOptionsBuilder WithClientName(string clientName)
    {
        _options.Connection.ClientProvidedName = clientName;
        return this;
    }

    #endregion

    #region Publisher Configuration

    /// <summary>
    /// Adds a new publisher configuration.
    /// </summary>
    /// <param name="name">Unique name for the publisher.</param>
    /// <param name="configure">Action to configure the publisher.</param>
    public RabbitXOptionsBuilder AddPublisher(string name, Action<PublisherOptionsBuilder> configure)
    {
        var builder = new PublisherOptionsBuilder();
        configure(builder);
        _options.Publishers[name] = builder.Build();
        return this;
    }

    /// <summary>
    /// Configures an existing publisher (loaded from configuration).
    /// </summary>
    /// <param name="name">Name of the publisher to configure.</param>
    /// <param name="configure">Action to modify the publisher configuration.</param>
    public RabbitXOptionsBuilder ConfigurePublisher(string name, Action<PublisherOptionsBuilder> configure)
    {
        if (!_options.Publishers.TryGetValue(name, out var existing))
        {
            throw new InvalidOperationException($"Publisher '{name}' not found. Use AddPublisher to create a new publisher.");
        }

        var builder = new PublisherOptionsBuilder(existing);
        configure(builder);
        _options.Publishers[name] = builder.Build();
        return this;
    }

    #endregion

    #region Consumer Configuration

    /// <summary>
    /// Adds a new consumer configuration.
    /// </summary>
    /// <param name="name">Unique name for the consumer.</param>
    /// <param name="configure">Action to configure the consumer.</param>
    public RabbitXOptionsBuilder AddConsumer(string name, Action<ConsumerOptionsBuilder> configure)
    {
        var builder = new ConsumerOptionsBuilder();
        configure(builder);
        _options.Consumers[name] = builder.Build();
        return this;
    }

    /// <summary>
    /// Configures an existing consumer (loaded from configuration).
    /// </summary>
    /// <param name="name">Name of the consumer to configure.</param>
    /// <param name="configure">Action to modify the consumer configuration.</param>
    public RabbitXOptionsBuilder ConfigureConsumer(string name, Action<ConsumerOptionsBuilder> configure)
    {
        if (!_options.Consumers.TryGetValue(name, out var existing))
        {
            throw new InvalidOperationException($"Consumer '{name}' not found. Use AddConsumer to create a new consumer.");
        }

        var builder = new ConsumerOptionsBuilder(existing);
        configure(builder);
        _options.Consumers[name] = builder.Build();
        return this;
    }

    #endregion

    #region RPC Configuration

    /// <summary>
    /// Adds a new RPC client configuration.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <param name="name">Unique name for the RPC client.</param>
    /// <param name="configure">Action to configure the RPC client.</param>
    public RabbitXOptionsBuilder AddRpcClient<TRequest, TResponse>(
        string name,
        Action<RpcClientOptionsBuilder> configure)
        where TRequest : class
        where TResponse : class
    {
        var builder = new RpcClientOptionsBuilder();
        configure(builder);
        var options = builder.Build();
        options.Name = name;
        options.RequestType = typeof(TRequest);
        options.ResponseType = typeof(TResponse);
        _options.RpcClients[name] = options;
        return this;
    }

    /// <summary>
    /// Adds a new RPC handler configuration.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="name">Unique name for the RPC handler.</param>
    /// <param name="configure">Action to configure the RPC handler.</param>
    public RabbitXOptionsBuilder AddRpcHandler<TRequest, TResponse, THandler>(
        string name,
        Action<RpcHandlerOptionsBuilder> configure)
        where TRequest : class
        where TResponse : class
        where THandler : class, IRpcHandler<TRequest, TResponse>
    {
        var builder = new RpcHandlerOptionsBuilder();
        configure(builder);
        var options = builder.Build();
        options.Name = name;
        options.RequestType = typeof(TRequest);
        options.ResponseType = typeof(TResponse);
        options.HandlerType = typeof(THandler);
        _options.RpcHandlers[name] = options;
        return this;
    }

    #endregion

    /// <summary>
    /// Builds the final RabbitX options.
    /// </summary>
    internal RabbitXOptions Build()
    {
        Validate();
        return _options;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(_options.Connection.HostName))
        {
            throw new InvalidOperationException("Connection hostname is required.");
        }

        foreach (var (name, publisher) in _options.Publishers)
        {
            if (string.IsNullOrWhiteSpace(publisher.Exchange))
            {
                throw new InvalidOperationException($"Publisher '{name}' requires an exchange.");
            }
        }

        foreach (var (name, consumer) in _options.Consumers)
        {
            if (string.IsNullOrWhiteSpace(consumer.Queue))
            {
                throw new InvalidOperationException($"Consumer '{name}' requires a queue.");
            }

            // Validate DLX configuration
            if (consumer.Retry.OnRetryExhausted == RetryExhaustedAction.SendToDeadLetter &&
                consumer.DeadLetter?.IsValid != true)
            {
                throw new InvalidOperationException(
                    $"Consumer '{name}' is configured to send to dead letter on retry exhausted, " +
                    "but DeadLetter is not properly configured.");
            }
        }

        // Validate RPC clients
        foreach (var (name, client) in _options.RpcClients)
        {
            if (string.IsNullOrWhiteSpace(client.Exchange))
            {
                throw new InvalidOperationException($"RPC client '{name}' requires an exchange.");
            }

            if (string.IsNullOrWhiteSpace(client.RoutingKey))
            {
                throw new InvalidOperationException($"RPC client '{name}' requires a routing key.");
            }
        }

        // Validate RPC handlers
        foreach (var (name, handler) in _options.RpcHandlers)
        {
            if (string.IsNullOrWhiteSpace(handler.Queue))
            {
                throw new InvalidOperationException($"RPC handler '{name}' requires a queue.");
            }

            if (handler.HandlerType == null)
            {
                throw new InvalidOperationException($"RPC handler '{name}' requires a handler type.");
            }
        }
    }
}
