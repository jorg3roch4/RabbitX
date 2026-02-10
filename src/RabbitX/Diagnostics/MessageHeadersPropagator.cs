using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace RabbitX.Diagnostics;

/// <summary>
/// Propagates W3C TraceContext through AMQP message headers for distributed tracing.
/// </summary>
internal static class MessageHeadersPropagator
{
    private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

    /// <summary>
    /// Injects the current trace context into AMQP message headers.
    /// Call this when publishing a message to propagate the trace to consumers.
    /// </summary>
    /// <param name="activity">The current activity (may be null if no trace is active).</param>
    /// <param name="headers">The AMQP message headers dictionary to inject into.</param>
    public static void Inject(Activity? activity, IDictionary<string, object?> headers)
    {
        if (activity is null)
            return;

        var context = new PropagationContext(activity.Context, Baggage.Current);
        Propagator.Inject(context, headers, InjectHeader);
    }

    /// <summary>
    /// Extracts a trace context from AMQP message headers.
    /// Call this when consuming a message to link the consumer span to the producer span.
    /// Accepts <see cref="IDictionary{TKey,TValue}"/> to match RabbitMQ.Client's header type.
    /// </summary>
    /// <param name="headers">The AMQP message headers (may be null).</param>
    /// <returns>The extracted ActivityContext, or default if no context was found.</returns>
    public static ActivityContext Extract(IDictionary<string, object?>? headers)
    {
        if (headers is null || headers.Count == 0)
            return default;

        var context = Propagator.Extract(default, headers, ExtractHeader);
        return context.ActivityContext;
    }

    private static void InjectHeader(IDictionary<string, object?> headers, string key, string value)
    {
        headers[key] = value;
    }

    private static IEnumerable<string> ExtractHeader(IDictionary<string, object?> headers, string key)
    {
        if (!headers.TryGetValue(key, out var value) || value is null)
            return [];

        // RabbitMQ.Client stores received header values as byte[]
        if (value is byte[] bytes)
            return [Encoding.UTF8.GetString(bytes)];

        if (value is string str)
            return [str];

        return [];
    }
}
