namespace RabbitX.Models;

/// <summary>
/// Result of message consumption indicating how to acknowledge the message.
/// </summary>
public enum ConsumeResult
{
    /// <summary>
    /// Message processed successfully. Removes the message from the queue.
    /// </summary>
    Ack,

    /// <summary>
    /// Transient failure occurred. The message will be retried according to the retry policy.
    /// After all retries are exhausted, the OnRetryExhausted action will be executed.
    /// </summary>
    Retry,

    /// <summary>
    /// Permanent failure occurred. The message will be sent to the dead letter queue immediately.
    /// Use this for validation errors or business rule violations that won't succeed on retry.
    /// </summary>
    Reject,

    /// <summary>
    /// Requeue the message for another consumer to process.
    /// Use this when the current consumer cannot process the message but another might.
    /// </summary>
    Defer
}
