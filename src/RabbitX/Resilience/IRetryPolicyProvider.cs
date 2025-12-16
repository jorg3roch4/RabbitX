using Polly;
using RabbitX.Configuration;

namespace RabbitX.Resilience;

/// <summary>
/// Provider for creating retry policies based on configuration.
/// </summary>
public interface IRetryPolicyProvider
{
    /// <summary>
    /// Gets the delay for a specific retry attempt.
    /// </summary>
    /// <param name="retryAttempt">The retry attempt number (1-based).</param>
    /// <param name="options">The retry configuration.</param>
    /// <returns>The delay to wait before this retry.</returns>
    TimeSpan GetDelayForRetry(int retryAttempt, RetryOptions options);

    /// <summary>
    /// Creates an async retry policy based on configuration.
    /// </summary>
    /// <param name="options">The retry configuration.</param>
    /// <param name="onRetry">Optional callback invoked on each retry.</param>
    /// <returns>An async Polly policy.</returns>
    IAsyncPolicy CreateRetryPolicy(
        RetryOptions options,
        Action<Exception, TimeSpan, int, Context>? onRetry = null);

    /// <summary>
    /// Creates an async retry policy with a typed result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="options">The retry configuration.</param>
    /// <param name="onRetry">Optional callback invoked on each retry.</param>
    /// <returns>An async Polly policy.</returns>
    IAsyncPolicy<TResult> CreateRetryPolicy<TResult>(
        RetryOptions options,
        Action<DelegateResult<TResult>, TimeSpan, int, Context>? onRetry = null);
}
