using RabbitX.Configuration;

namespace RabbitX.Builders;

/// <summary>
/// Fluent builder for configuring retry policy options.
/// </summary>
public sealed class RetryOptionsBuilder
{
    private readonly RetryOptions _options;

    /// <summary>
    /// Creates a new builder with default options.
    /// </summary>
    public RetryOptionsBuilder()
    {
        _options = new RetryOptions();
    }

    /// <summary>
    /// Creates a builder from existing options.
    /// </summary>
    internal RetryOptionsBuilder(RetryOptions existing)
    {
        _options = new RetryOptions
        {
            MaxRetries = existing.MaxRetries,
            DelaysInSeconds = existing.DelaysInSeconds?.ToArray(),
            InitialDelaySeconds = existing.InitialDelaySeconds,
            MaxDelaySeconds = existing.MaxDelaySeconds,
            BackoffMultiplier = existing.BackoffMultiplier,
            UseJitter = existing.UseJitter,
            OnRetryExhausted = existing.OnRetryExhausted
        };
    }

    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    public RetryOptionsBuilder MaxRetries(int maxRetries)
    {
        _options.MaxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Disables retries.
    /// </summary>
    public RetryOptionsBuilder NoRetries()
    {
        _options.MaxRetries = 0;
        return this;
    }

    /// <summary>
    /// Sets specific delays for each retry attempt.
    /// This takes priority over exponential backoff.
    /// </summary>
    /// <param name="delays">The delays for each retry attempt.</param>
    /// <example>
    /// .WithDelays(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1))
    /// </example>
    public RetryOptionsBuilder WithDelays(params TimeSpan[] delays)
    {
        _options.DelaysInSeconds = delays.Select(d => (int)d.TotalSeconds).ToArray();
        _options.MaxRetries = delays.Length;
        return this;
    }

    /// <summary>
    /// Sets specific delays in seconds for each retry attempt.
    /// </summary>
    public RetryOptionsBuilder WithDelaysInSeconds(params int[] delaysInSeconds)
    {
        _options.DelaysInSeconds = delaysInSeconds;
        _options.MaxRetries = delaysInSeconds.Length;
        return this;
    }

    /// <summary>
    /// Configures exponential backoff for retries.
    /// </summary>
    /// <param name="initialDelay">The delay for the first retry.</param>
    /// <param name="maxDelay">The maximum delay between retries.</param>
    /// <param name="multiplier">The multiplier for each subsequent retry (default 2.0).</param>
    public RetryOptionsBuilder ExponentialBackoff(
        TimeSpan initialDelay,
        TimeSpan maxDelay,
        double multiplier = 2.0)
    {
        _options.DelaysInSeconds = null; // Clear specific delays
        _options.InitialDelaySeconds = (int)initialDelay.TotalSeconds;
        _options.MaxDelaySeconds = (int)maxDelay.TotalSeconds;
        _options.BackoffMultiplier = multiplier;
        return this;
    }

    /// <summary>
    /// Configures linear backoff (constant delay between retries).
    /// </summary>
    public RetryOptionsBuilder LinearBackoff(TimeSpan delay)
    {
        _options.DelaysInSeconds = null;
        _options.InitialDelaySeconds = (int)delay.TotalSeconds;
        _options.MaxDelaySeconds = (int)delay.TotalSeconds;
        _options.BackoffMultiplier = 1.0;
        return this;
    }

    /// <summary>
    /// Enables jitter to add random variation to delays.
    /// Helps prevent thundering herd problem.
    /// </summary>
    public RetryOptionsBuilder WithJitter(bool useJitter = true)
    {
        _options.UseJitter = useJitter;
        return this;
    }

    /// <summary>
    /// Disables jitter.
    /// </summary>
    public RetryOptionsBuilder NoJitter()
    {
        _options.UseJitter = false;
        return this;
    }

    /// <summary>
    /// Configures what to do when all retries are exhausted.
    /// </summary>
    public RetryOptionsBuilder OnExhausted(RetryExhaustedAction action)
    {
        _options.OnRetryExhausted = action;
        return this;
    }

    /// <summary>
    /// When retries are exhausted, send the message to the dead letter queue.
    /// </summary>
    public RetryOptionsBuilder ThenSendToDeadLetter()
    {
        _options.OnRetryExhausted = RetryExhaustedAction.SendToDeadLetter;
        return this;
    }

    /// <summary>
    /// When retries are exhausted, requeue the message.
    /// Warning: May cause infinite loops.
    /// </summary>
    public RetryOptionsBuilder ThenRequeue()
    {
        _options.OnRetryExhausted = RetryExhaustedAction.Requeue;
        return this;
    }

    /// <summary>
    /// When retries are exhausted, discard the message.
    /// </summary>
    public RetryOptionsBuilder ThenDiscard()
    {
        _options.OnRetryExhausted = RetryExhaustedAction.Discard;
        return this;
    }

    /// <summary>
    /// Builds the retry options.
    /// </summary>
    internal RetryOptions Build() => _options;
}
