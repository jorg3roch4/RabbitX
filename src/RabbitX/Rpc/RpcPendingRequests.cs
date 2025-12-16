using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RabbitX.Interfaces;

namespace RabbitX.Rpc;

/// <summary>
/// Thread-safe implementation of IRpcPendingRequests with automatic cleanup of expired requests.
/// </summary>
public sealed class RpcPendingRequests : IRpcPendingRequests
{
    private readonly ConcurrentDictionary<string, PendingRpcRequest> _pending = new();
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<RpcPendingRequests> _logger;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of RpcPendingRequests.
    /// </summary>
    public RpcPendingRequests(IMessageSerializer serializer, ILogger<RpcPendingRequests> logger)
    {
        _serializer = serializer;
        _logger = logger;
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <inheritdoc />
    public int PendingCount => _pending.Count;

    /// <inheritdoc />
    public void Register<TResponse>(string correlationId, TaskCompletionSource<TResponse> tcs, TimeSpan timeout)
        where TResponse : class
    {
        var pending = new PendingRpcRequest(typeof(TResponse), tcs, timeout);
        _pending[correlationId] = pending;

        _logger.LogDebug(
            "Registered RPC request {CorrelationId}, pending count: {Count}",
            correlationId,
            _pending.Count);
    }

    /// <inheritdoc />
    public bool TryComplete(string correlationId, ReadOnlyMemory<byte> responseBody)
    {
        if (!_pending.TryRemove(correlationId, out var pending))
        {
            _logger.LogWarning(
                "RPC response received for unknown correlation ID: {CorrelationId}",
                correlationId);
            return false;
        }

        try
        {
            var response = _serializer.Deserialize(responseBody, pending.ResponseType);
            pending.Complete(response);

            _logger.LogDebug(
                "Completed RPC request {CorrelationId}, remaining pending: {Count}",
                correlationId,
                _pending.Count);

            return true;
        }
        catch (Exception ex)
        {
            pending.Fail(ex);

            _logger.LogError(
                ex,
                "Failed to deserialize RPC response for {CorrelationId}",
                correlationId);

            return false;
        }
    }

    /// <inheritdoc />
    public bool TryFail(string correlationId, Exception exception)
    {
        if (!_pending.TryRemove(correlationId, out var pending))
            return false;

        pending.Fail(exception);

        _logger.LogDebug(
            "Failed RPC request {CorrelationId}: {Error}",
            correlationId,
            exception.Message);

        return true;
    }

    /// <inheritdoc />
    public bool TryRemove(string correlationId)
    {
        var removed = _pending.TryRemove(correlationId, out _);

        if (removed)
        {
            _logger.LogDebug(
                "Removed RPC request {CorrelationId}, remaining pending: {Count}",
                correlationId,
                _pending.Count);
        }

        return removed;
    }

    /// <inheritdoc />
    public bool TryGet(string correlationId, out PendingRpcRequest? pending)
        => _pending.TryGetValue(correlationId, out pending);

    private void CleanupExpired(object? state)
    {
        if (_disposed)
            return;

        var expiredCount = 0;

        foreach (var kvp in _pending)
        {
            if (kvp.Value.IsExpired)
            {
                if (_pending.TryRemove(kvp.Key, out var pending))
                {
                    pending.Fail(new TimeoutException($"RPC request {kvp.Key} expired after {pending.Timeout.TotalSeconds}s"));
                    expiredCount++;

                    _logger.LogWarning(
                        "Cleaned up expired RPC request {CorrelationId}",
                        kvp.Key);
                }
            }
        }

        if (expiredCount > 0)
        {
            _logger.LogInformation(
                "Cleaned up {ExpiredCount} expired RPC requests, remaining: {Count}",
                expiredCount,
                _pending.Count);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer.Dispose();

        // Cancel all pending requests
        foreach (var kvp in _pending)
        {
            if (_pending.TryRemove(kvp.Key, out var pending))
            {
                pending.Cancel();
            }
        }
    }
}
