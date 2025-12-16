using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitX.Builders;
using RabbitX.Configuration;
using RabbitX.Extensions;
using RabbitX.Samples.Common.Configuration;
using RabbitX.Samples.Common.Handlers;
using RabbitX.Samples.Common.Messages;

namespace RabbitX.Samples.Common.Extensions;

/// <summary>
/// Extension methods for configuring RabbitX based on sample configuration.
/// </summary>
public static class RabbitXConfigurationExtensions
{
    /// <summary>
    /// Configures RabbitX for publisher scenarios.
    /// </summary>
    public static IServiceCollection AddRabbitXPublisher(
        this IServiceCollection services,
        IConfiguration configuration,
        SampleConfiguration sampleConfig)
    {
        if (sampleConfig.ConfigMode == ConfigurationMode.AppSettings)
        {
            return services.AddRabbitX(configuration);
        }

        return services.AddRabbitX(options =>
        {
            options
                .UseConnection("localhost", 5672)
                .UseCredentials("rabbit", "rabbit")
                .UseVirtualHost("/")
                .WithClientName("RabbitX.Sample.Publisher");

            // Task Publisher
            options.AddPublisher("TaskPublisher", pub => pub
                .ToExchange("tasks.exchange", "direct")
                .WithRoutingKey("task.process")
                .Durable()
                .Persistent());

            if (sampleConfig.ConsumerScenario == ConsumerScenario.Multiple)
            {
                // Email Publisher
                options.AddPublisher("EmailPublisher", pub => pub
                    .ToExchange("emails.exchange", "direct")
                    .WithRoutingKey("email.send")
                    .Durable()
                    .Persistent());

                // Report Publisher
                options.AddPublisher("ReportPublisher", pub => pub
                    .ToExchange("reports.exchange", "direct")
                    .WithRoutingKey("report.generate")
                    .Durable()
                    .Persistent());
            }
        });
    }

    /// <summary>
    /// Configures RabbitX for consumer scenarios.
    /// </summary>
    public static IServiceCollection AddRabbitXConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        SampleConfiguration sampleConfig)
    {
        if (sampleConfig.ConfigMode == ConfigurationMode.AppSettings)
        {
            services.AddRabbitX(configuration);
        }
        else
        {
            services.AddRabbitX(options =>
            {
                options
                    .UseConnection("localhost", 5672)
                    .UseCredentials("rabbit", "rabbit")
                    .UseVirtualHost("/")
                    .WithClientName("RabbitX.Sample.Consumer");

                ConfigureConsumers(options, sampleConfig);
            });
        }

        // Register handlers based on scenario
        RegisterHandlers(services, sampleConfig);

        return services;
    }

    private static void ConfigureConsumers(
        RabbitXOptionsBuilder options,
        SampleConfiguration config)
    {
        switch (config.ConsumerScenario)
        {
            case ConsumerScenario.Single:
                ConfigureTaskConsumer(options, config);
                break;

            case ConsumerScenario.Multiple:
                ConfigureTaskConsumer(options, config);
                ConfigureEmailConsumer(options, config);
                ConfigureReportConsumer(options, config);
                break;

            case ConsumerScenario.DeadLetterProcessor:
                ConfigureDeadLetterConsumer(options, config);
                break;
        }
    }

    private static void ConfigureTaskConsumer(
        RabbitXOptionsBuilder options,
        SampleConfiguration config)
    {
        options.AddConsumer("TaskConsumer", con => con
            .FromQueue("tasks.queue")
            .BindToExchange("tasks.exchange", "task.process")
            .WithPrefetch(10)
            .WithRetry(retry => ConfigureRetry(retry, config))
            .WithDeadLetterIf(
                config.OnRetryExhausted == RetryExhaustedActionType.SendToDeadLetter,
                dlx => dlx
                    .Exchange("tasks.dlx")
                    .RoutingKey("task.failed")
                    .Queue("tasks.dlq")));
    }

    private static void ConfigureEmailConsumer(
        RabbitXOptionsBuilder options,
        SampleConfiguration config)
    {
        options.AddConsumer("EmailConsumer", con => con
            .FromQueue("emails.queue")
            .BindToExchange("emails.exchange", "email.send")
            .WithPrefetch(5)
            .WithRetry(retry => ConfigureRetry(retry, config))
            .WithDeadLetterIf(
                config.OnRetryExhausted == RetryExhaustedActionType.SendToDeadLetter,
                dlx => dlx
                    .Exchange("emails.dlx")
                    .RoutingKey("email.failed")
                    .Queue("emails.dlq")));
    }

    private static void ConfigureReportConsumer(
        RabbitXOptionsBuilder options,
        SampleConfiguration config)
    {
        options.AddConsumer("ReportConsumer", con => con
            .FromQueue("reports.queue")
            .BindToExchange("reports.exchange", "report.generate")
            .WithPrefetch(5)
            .WithRetry(retry => ConfigureRetry(retry, config))
            .WithDeadLetterIf(
                config.OnRetryExhausted == RetryExhaustedActionType.SendToDeadLetter,
                dlx => dlx
                    .Exchange("reports.dlx")
                    .RoutingKey("report.failed")
                    .Queue("reports.dlq")));
    }

    private static void ConfigureDeadLetterConsumer(
        RabbitXOptionsBuilder options,
        SampleConfiguration config)
    {
        // Consumer for tasks DLQ
        options.AddConsumer("TasksDLQConsumer", con => con
            .FromQueue("tasks.dlq")
            .WithPrefetch(1)
            .WithRetry(retry => retry.MaxRetries(0).ThenDiscard()));

        // Consumer for emails DLQ
        options.AddConsumer("EmailsDLQConsumer", con => con
            .FromQueue("emails.dlq")
            .WithPrefetch(1)
            .WithRetry(retry => retry.MaxRetries(0).ThenDiscard()));

        // Consumer for reports DLQ
        options.AddConsumer("ReportsDLQConsumer", con => con
            .FromQueue("reports.dlq")
            .WithPrefetch(1)
            .WithRetry(retry => retry.MaxRetries(0).ThenDiscard()));
    }

    private static void ConfigureRetry(
        RetryOptionsBuilder retry,
        SampleConfiguration config)
    {
        switch (config.RetryStrategy)
        {
            case RetryStrategyType.SpecificDelays:
                retry.WithDelays(
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(60),
                    TimeSpan.FromSeconds(120),
                    TimeSpan.FromSeconds(300));
                break;

            case RetryStrategyType.ExponentialBackoff:
                retry
                    .MaxRetries(5)
                    .ExponentialBackoff(
                        initialDelay: TimeSpan.FromSeconds(5),
                        maxDelay: TimeSpan.FromMinutes(5),
                        multiplier: 2.0)
                    .WithJitter();
                break;

            case RetryStrategyType.NoRetry:
                retry.MaxRetries(0);
                break;
        }

        switch (config.OnRetryExhausted)
        {
            case RetryExhaustedActionType.SendToDeadLetter:
                retry.ThenSendToDeadLetter();
                break;

            case RetryExhaustedActionType.Requeue:
                retry.ThenRequeue();
                break;

            case RetryExhaustedActionType.Discard:
                retry.ThenDiscard();
                break;
        }
    }

    private static void RegisterHandlers(
        IServiceCollection services,
        SampleConfiguration config)
    {
        switch (config.ConsumerScenario)
        {
            case ConsumerScenario.Single:
                services.AddMessageHandler<TaskMessage, TaskMessageHandler>();
                services.AddHostedConsumer<TaskMessage>("TaskConsumer");
                break;

            case ConsumerScenario.Multiple:
                services.AddMessageHandler<TaskMessage, TaskMessageHandler>();
                services.AddMessageHandler<EmailMessage, EmailMessageHandler>();
                services.AddMessageHandler<ReportMessage, ReportMessageHandler>();

                services.AddHostedConsumer<TaskMessage>("TaskConsumer");
                services.AddHostedConsumer<EmailMessage>("EmailConsumer");
                services.AddHostedConsumer<ReportMessage>("ReportConsumer");
                break;

            case ConsumerScenario.DeadLetterProcessor:
                services.AddMessageHandler<DeadLetterMessage, DeadLetterMessageHandler>();
                services.AddHostedConsumer<DeadLetterMessage>("TasksDLQConsumer");
                services.AddHostedConsumer<DeadLetterMessage>("EmailsDLQConsumer");
                services.AddHostedConsumer<DeadLetterMessage>("ReportsDLQConsumer");
                break;
        }
    }
}

/// <summary>
/// Extension for conditional dead letter configuration.
/// </summary>
public static class ConsumerOptionsBuilderExtensions
{
    public static ConsumerOptionsBuilder WithDeadLetterIf(
        this ConsumerOptionsBuilder builder,
        bool condition,
        Action<DeadLetterOptionsBuilder> configure)
    {
        if (condition)
        {
            builder.WithDeadLetter(configure);
        }

        return builder;
    }
}
