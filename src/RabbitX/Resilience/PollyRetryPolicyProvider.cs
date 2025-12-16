using Microsoft.Extensions.Logging;
using Polly;
using RabbitX.Configuration;

namespace RabbitX.Resilience;

/// <summary>
/// Polly-based implementation of retry policy provider.
/// </summary>
public sealed class PollyRetryPolicyProvider : IRetryPolicyProvider
{
    private readonly ILogger<PollyRetryPolicyProvider> _logger;
    private readonly Random _jitterRandom = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PollyRetryPolicyProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public PollyRetryPolicyProvider(ILogger<PollyRetryPolicyProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TimeSpan GetDelayForRetry(int retryAttempt, RetryOptions options)
    {
        if (retryAttempt <= 0)
            return TimeSpan.Zero;

        double delaySeconds;

        // Use specific delays if defined
        if (options.DelaysInSeconds is { Length: > 0 })
        {
            var index = Math.Min(retryAttempt - 1, options.DelaysInSeconds.Length - 1);
            delaySeconds = options.DelaysInSeconds[index];
        }
        else
        {
            // Calculate exponential backoff
            delaySeconds = options.InitialDelaySeconds * Math.Pow(options.BackoffMultiplier, retryAttempt - 1);
            delaySeconds = Math.Min(delaySeconds, options.MaxDelaySeconds);
        }

        // Apply jitter if enabled (±20%)
        if (options.UseJitter)
        {
            var jitterFactor = 0.8 + (_jitterRandom.NextDouble() * 0.4);
            delaySeconds *= jitterFactor;
        }

        return TimeSpan.FromSeconds(delaySeconds);
    }

    /// <inheritdoc />
    public IAsyncPolicy CreateRetryPolicy(
        RetryOptions options,
        Action<Exception, TimeSpan, int, Context>? onRetry = null)
    {
        if (!options.IsEnabled)
        {
            return Policy.NoOpAsync();
        }

        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: options.MaxRetries,
                sleepDurationProvider: (retryAttempt, context) => GetDelayForRetry(retryAttempt, options),
                onRetry: (exception, delay, retryAttempt, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {Attempt}/{MaxRetries} after {Delay}s. Error: {Error}",
                        retryAttempt,
                        options.MaxRetries,
                        delay.TotalSeconds,
                        exception.Message);

                    onRetry?.Invoke(exception, delay, retryAttempt, context);
                });
    }

    /// <inheritdoc />
    public IAsyncPolicy<TResult> CreateRetryPolicy<TResult>(
        RetryOptions options,
        Action<DelegateResult<TResult>, TimeSpan, int, Context>? onRetry = null)
    {
        if (!options.IsEnabled)
        {
            return Policy.NoOpAsync<TResult>();
        }

        return Policy<TResult>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: options.MaxRetries,
                sleepDurationProvider: (retryAttempt, context) => GetDelayForRetry(retryAttempt, options),
                onRetry: (outcome, delay, retryAttempt, context) =>
                {
                    if (outcome.Exception != null)
                    {
                        _logger.LogWarning(
                            outcome.Exception,
                            "Retry {Attempt}/{MaxRetries} after {Delay}s. Error: {Error}",
                            retryAttempt,
                            options.MaxRetries,
                            delay.TotalSeconds,
                            outcome.Exception.Message);
                    }

                    onRetry?.Invoke(outcome, delay, retryAttempt, context);
                });
    }
}
