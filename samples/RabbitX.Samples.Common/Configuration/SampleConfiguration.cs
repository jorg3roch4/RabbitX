namespace RabbitX.Samples.Common.Configuration;

/// <summary>
/// Unified configuration for all sample scenarios.
/// </summary>
public sealed class SampleConfiguration
{
    public ConfigurationMode ConfigMode { get; set; } = ConfigurationMode.FluentApi;
    public RetryStrategyType RetryStrategy { get; set; } = RetryStrategyType.SpecificDelays;
    public RetryExhaustedActionType OnRetryExhausted { get; set; } = RetryExhaustedActionType.SendToDeadLetter;
    public PublisherMode PublisherMode { get; set; } = PublisherMode.Reliable;
    public ConsumerScenario ConsumerScenario { get; set; } = ConsumerScenario.Multiple;
    public int MessageCount { get; set; } = 0; // 0 = interactive, >0 = non-interactive
    public bool NonInteractive { get; set; } = false;

    /// <summary>
    /// Gets a display-friendly name for the current configuration.
    /// </summary>
    public string GetDisplayName()
    {
        return $"{ConfigMode} | {RetryStrategy} | {OnRetryExhausted}";
    }

    /// <summary>
    /// Gets the specific delays for retry (when using SpecificDelays strategy).
    /// </summary>
    public int[] GetSpecificDelays() => new[] { 10, 30, 60, 120, 300 };

    /// <summary>
    /// Gets the configuration key suffix based on current settings.
    /// </summary>
    public string GetConfigurationKey()
    {
        var retry = RetryStrategy switch
        {
            RetryStrategyType.SpecificDelays => "Specific",
            RetryStrategyType.ExponentialBackoff => "Exponential",
            _ => "NoRetry"
        };

        var action = OnRetryExhausted switch
        {
            RetryExhaustedActionType.SendToDeadLetter => "DLX",
            RetryExhaustedActionType.Requeue => "Requeue",
            _ => "Discard"
        };

        return $"{retry}_{action}";
    }
}

/// <summary>
/// Configuration source mode.
/// </summary>
public enum ConfigurationMode
{
    /// <summary>
    /// Configuration via code using Fluent API builders.
    /// </summary>
    FluentApi,

    /// <summary>
    /// Configuration via appsettings.json file.
    /// </summary>
    AppSettings
}

/// <summary>
/// Retry strategy type.
/// </summary>
public enum RetryStrategyType
{
    /// <summary>
    /// Use specific delays: 10s, 30s, 60s, 120s, 300s.
    /// </summary>
    SpecificDelays,

    /// <summary>
    /// Use exponential backoff: 5s * 2^n, max 5 minutes.
    /// </summary>
    ExponentialBackoff,

    /// <summary>
    /// No retry - fail immediately.
    /// </summary>
    NoRetry
}

/// <summary>
/// Action when retries are exhausted.
/// </summary>
public enum RetryExhaustedActionType
{
    /// <summary>
    /// Send message to Dead Letter Exchange.
    /// </summary>
    SendToDeadLetter,

    /// <summary>
    /// Requeue message to original queue.
    /// </summary>
    Requeue,

    /// <summary>
    /// Discard message (acknowledge without processing).
    /// </summary>
    Discard
}

/// <summary>
/// Publisher operation mode.
/// </summary>
public enum PublisherMode
{
    /// <summary>
    /// Fire-and-forget publishing (no confirms).
    /// </summary>
    Basic,

    /// <summary>
    /// Reliable publishing with broker confirms.
    /// </summary>
    Reliable
}

/// <summary>
/// Consumer scenario type.
/// </summary>
public enum ConsumerScenario
{
    /// <summary>
    /// Single consumer (payments only).
    /// </summary>
    Single,

    /// <summary>
    /// Multiple consumers (payments + notifications + orders).
    /// </summary>
    Multiple,

    /// <summary>
    /// Dead letter processor consumer.
    /// </summary>
    DeadLetterProcessor
}
