using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitX.Interfaces;

namespace RabbitX.Consumers;

/// <summary>
/// Background service that hosts a message consumer.
/// </summary>
/// <typeparam name="TMessage">The type of message to consume.</typeparam>
public sealed class ConsumerHostedService<TMessage> : BackgroundService
    where TMessage : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _consumerName;
    private readonly ILogger<ConsumerHostedService<TMessage>> _logger;

    private IMessageConsumer<TMessage>? _consumer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerHostedService{TMessage}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="consumerName">The name of the consumer configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public ConsumerHostedService(
        IServiceProvider serviceProvider,
        string consumerName,
        ILogger<ConsumerHostedService<TMessage>> logger)
    {
        _serviceProvider = serviceProvider;
        _consumerName = consumerName;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting consumer hosted service for {Consumer} (Message type: {MessageType})",
            _consumerName,
            typeof(TMessage).Name);

        try
        {
            // Create scope for resolving scoped dependencies
            using var scope = _serviceProvider.CreateScope();

            var consumerFactory = scope.ServiceProvider.GetRequiredService<IConsumerFactory>();
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();

            _consumer = consumerFactory.CreateConsumer(_consumerName, handler);

            await _consumer.StartAsync(stoppingToken);

            // Keep running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                // Log heartbeat
                if (_consumer.IsRunning)
                {
                    _logger.LogDebug("Consumer {Consumer} is running", _consumerName);
                }
                else
                {
                    _logger.LogWarning("Consumer {Consumer} is not running, attempting restart", _consumerName);
                    await _consumer.StartAsync(stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Consumer hosted service {Consumer} is stopping", _consumerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in consumer hosted service {Consumer}", _consumerName);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping consumer hosted service {Consumer}", _consumerName);

        if (_consumer is not null)
        {
            await _consumer.StopAsync(cancellationToken);
            await _consumer.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
