namespace RabbitX.Configuration;

/// <summary>
/// Configuration for message retry policies.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts (0 = no retries).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Specific delays for each retry attempt in seconds.
    /// If defined, takes priority over exponential backoff.
    /// Example: [10, 30, 60, 120, 300] means 10s for 1st retry, 30s for 2nd, etc.
    /// </summary>
    public int[]? DelaysInSeconds { get; set; }

    /// <summary>
    /// Initial delay for exponential backoff in seconds.
    /// Used when DelaysInSeconds is not defined.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Maximum delay for exponential backoff in seconds.
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 300;

    /// <summary>
    /// Multiplier for exponential backoff (e.g., 2.0 = double each time).
    /// Formula: InitialDelay * (Multiplier ^ (attempt - 1))
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to add random jitter to delays to prevent thundering herd.
    /// Adds ±20% variation to the calculated delay.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Action to take when all retries are exhausted.
    /// </summary>
    public RetryExhaustedAction OnRetryExhausted { get; set; } = RetryExhaustedAction.SendToDeadLetter;

    /// <summary>
    /// Gets the delay for a specific retry attempt.
    /// </summary>
    /// <param name="attempt">The retry attempt number (1-based).</param>
    /// <returns>The delay in seconds.</returns>
    public TimeSpan GetDelayForAttempt(int attempt)
    {
        if (attempt <= 0) return TimeSpan.Zero;

        double delaySeconds;

        // Use specific delays if defined
        if (DelaysInSeconds is { Length: > 0 })
        {
            var index = Math.Min(attempt - 1, DelaysInSeconds.Length - 1);
            delaySeconds = DelaysInSeconds[index];
        }
        else
        {
            // Use exponential backoff
            delaySeconds = InitialDelaySeconds * Math.Pow(BackoffMultiplier, attempt - 1);
            delaySeconds = Math.Min(delaySeconds, MaxDelaySeconds);
        }

        return TimeSpan.FromSeconds(delaySeconds);
    }

    /// <summary>
    /// Checks if retries are enabled.
    /// </summary>
    public bool IsEnabled => MaxRetries > 0;
}

/// <summary>
/// Action to take when all retry attempts are exhausted.
/// </summary>
public enum RetryExhaustedAction
{
    /// <summary>
    /// Send the message to the Dead Letter Exchange.
    /// Requires DeadLetter to be configured.
    /// </summary>
    SendToDeadLetter,

    /// <summary>
    /// Requeue the message to be processed again later.
    /// Warning: May cause infinite loops if the issue persists.
    /// </summary>
    Requeue,

    /// <summary>
    /// Discard the message (acknowledge without processing).
    /// The message will be logged and lost.
    /// </summary>
    Discard
}
