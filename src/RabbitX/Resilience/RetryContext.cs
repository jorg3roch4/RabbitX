using RabbitX.Configuration;

namespace RabbitX.Resilience;

/// <summary>
/// Context for tracking retry state during message processing.
/// </summary>
public sealed class RetryContext
{
    /// <summary>
    /// The current retry attempt (0 for first attempt).
    /// </summary>
    public int CurrentAttempt { get; private set; }

    /// <summary>
    /// The retry configuration.
    /// </summary>
    public RetryOptions Options { get; }

    /// <summary>
    /// The message ID being processed.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// History of exceptions from previous attempts.
    /// </summary>
    public List<Exception> ExceptionHistory { get; } = new();

    /// <summary>
    /// Total time spent in retries.
    /// </summary>
    public TimeSpan TotalRetryTime { get; private set; }

    /// <summary>
    /// When the first attempt started.
    /// </summary>
    public DateTimeOffset FirstAttemptTime { get; }

    /// <summary>
    /// Custom data that can be passed between retry attempts.
    /// </summary>
    public Dictionary<string, object> Data { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryContext"/> class.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message being retried.</param>
    /// <param name="options">The retry options to apply.</param>
    public RetryContext(string messageId, RetryOptions options)
    {
        MessageId = messageId;
        Options = options;
        FirstAttemptTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Whether more retries are available.
    /// </summary>
    public bool CanRetry => CurrentAttempt < Options.MaxRetries;

    /// <summary>
    /// Whether this is the first attempt.
    /// </summary>
    public bool IsFirstAttempt => CurrentAttempt == 0;

    /// <summary>
    /// Whether all retries have been exhausted.
    /// </summary>
    public bool IsExhausted => CurrentAttempt >= Options.MaxRetries;

    /// <summary>
    /// Gets the delay for the next retry attempt.
    /// </summary>
    public TimeSpan GetNextDelay()
    {
        return Options.GetDelayForAttempt(CurrentAttempt + 1);
    }

    /// <summary>
    /// Records a failed attempt and increments the counter.
    /// </summary>
    public void RecordFailure(Exception exception, TimeSpan delayUsed)
    {
        ExceptionHistory.Add(exception);
        TotalRetryTime += delayUsed;
        CurrentAttempt++;
    }

    /// <summary>
    /// Creates a context from message headers (for rehydrating retry state).
    /// </summary>
    public static RetryContext FromHeaders(
        string messageId,
        RetryOptions options,
        IReadOnlyDictionary<string, object> headers)
    {
        var context = new RetryContext(messageId, options);

        if (headers.TryGetValue("x-retry-count", out var retryCount))
        {
            context.CurrentAttempt = Convert.ToInt32(retryCount);
        }

        return context;
    }

    /// <summary>
    /// Converts retry state to headers for message republishing.
    /// </summary>
    public Dictionary<string, object> ToHeaders()
    {
        return new Dictionary<string, object>
        {
            ["x-retry-count"] = CurrentAttempt,
            ["x-first-attempt-time"] = FirstAttemptTime.ToUnixTimeSeconds(),
            ["x-total-retry-time-ms"] = (long)TotalRetryTime.TotalMilliseconds,
            ["x-last-error"] = ExceptionHistory.LastOrDefault()?.Message ?? string.Empty
        };
    }
}
