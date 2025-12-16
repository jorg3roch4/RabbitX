using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitX.Builders;
using RabbitX.Configuration;
using RabbitX.Connection;
using RabbitX.Consumers;
using RabbitX.Interfaces;
using RabbitX.Publishers;
using RabbitX.Resilience;
using RabbitX.Rpc;
using RabbitX.Serialization;

namespace RabbitX.Extensions;

/// <summary>
/// Extension methods for registering RabbitX services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitX services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure RabbitX options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRabbitX(
        this IServiceCollection services,
        Action<RabbitXOptionsBuilder> configure)
    {
        var builder = new RabbitXOptionsBuilder();
        configure(builder);
        var options = builder.Build();

        // Register options
        services.AddSingleton(options);

        // Register connection (singleton for connection pooling)
        services.AddSingleton<IRabbitMQConnection, RabbitMQConnection>();

        // Register serializer
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

        // Register resilience
        services.AddSingleton<IRetryPolicyProvider, PollyRetryPolicyProvider>();

        // Register factories
        services.AddSingleton<IPublisherFactory, RabbitMQPublisherFactory>();
        services.AddSingleton<IConsumerFactory, RabbitMQConsumerFactory>();

        // Register RPC services
        services.AddSingleton<IRpcPendingRequests, RpcPendingRequests>();
        services.AddSingleton<IRpcClientFactory, RabbitMQRpcClientFactory>();

        // Register RPC handlers
        foreach (var handler in options.RpcHandlers.Values)
        {
            if (handler.HandlerType != null)
            {
                services.AddScoped(handler.HandlerType);
            }
        }

        // Add RPC consumer hosted service if there are handlers
        if (options.RpcHandlers.Any())
        {
            services.AddHostedService<RpcConsumerHostedService>();
        }

        return services;
    }

    /// <summary>
    /// Adds RabbitX services to the service collection using configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing RabbitX settings.</param>
    /// <param name="sectionName">The configuration section name (default: "RabbitX").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRabbitX(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "RabbitX")
    {
        var options = new RabbitXOptions();
        configuration.GetSection(sectionName).Bind(options);

        // Register options
        services.AddSingleton(options);

        // Register connection (singleton for connection pooling)
        services.AddSingleton<IRabbitMQConnection, RabbitMQConnection>();

        // Register serializer
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

        // Register resilience
        services.AddSingleton<IRetryPolicyProvider, PollyRetryPolicyProvider>();

        // Register factories
        services.AddSingleton<IPublisherFactory, RabbitMQPublisherFactory>();
        services.AddSingleton<IConsumerFactory, RabbitMQConsumerFactory>();

        // Register RPC services
        services.AddSingleton<IRpcPendingRequests, RpcPendingRequests>();
        services.AddSingleton<IRpcClientFactory, RabbitMQRpcClientFactory>();

        // Register RPC handlers from configuration (without type info, handlers must be registered separately)
        if (options.RpcHandlers.Any())
        {
            services.AddHostedService<RpcConsumerHostedService>();
        }

        return services;
    }

    /// <summary>
    /// Adds a custom message serializer.
    /// </summary>
    /// <typeparam name="TSerializer">The serializer type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRabbitXSerializer<TSerializer>(this IServiceCollection services)
        where TSerializer : class, IMessageSerializer
    {
        // Remove existing serializer
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessageSerializer));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton<IMessageSerializer, TSerializer>();
        return services;
    }

    /// <summary>
    /// Registers a message handler for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddScoped<IMessageHandler<TMessage>, THandler>();
        return services;
    }

    /// <summary>
    /// Registers a message handler instance.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="handler">The handler instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageHandler<TMessage>(
        this IServiceCollection services,
        IMessageHandler<TMessage> handler)
        where TMessage : class
    {
        services.AddSingleton(handler);
        return services;
    }

    /// <summary>
    /// Registers a consumer as a hosted service.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="consumerName">The name of the consumer configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHostedConsumer<TMessage>(
        this IServiceCollection services,
        string consumerName)
        where TMessage : class
    {
        services.AddSingleton<IHostedService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConsumerHostedService<TMessage>>>();
            return new ConsumerHostedService<TMessage>(sp, consumerName, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers multiple consumers as hosted services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="consumerConfigurations">Dictionary of consumer name to message type.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHostedConsumers(
        this IServiceCollection services,
        params (string ConsumerName, Type MessageType)[] consumerConfigurations)
    {
        foreach (var (consumerName, messageType) in consumerConfigurations)
        {
            var method = typeof(ServiceCollectionExtensions)
                .GetMethod(nameof(AddHostedConsumer))!
                .MakeGenericMethod(messageType);

            method.Invoke(null, new object[] { services, consumerName });
        }

        return services;
    }

    /// <summary>
    /// Registers an RPC handler for a specific request/response type pair.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRpcHandler<TRequest, TResponse, THandler>(this IServiceCollection services)
        where TRequest : class
        where TResponse : class
        where THandler : class, IRpcHandler<TRequest, TResponse>
    {
        services.AddScoped<THandler>();
        services.AddScoped<IRpcHandler<TRequest, TResponse>, THandler>();
        return services;
    }
}
