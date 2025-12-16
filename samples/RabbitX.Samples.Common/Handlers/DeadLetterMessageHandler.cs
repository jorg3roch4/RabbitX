using Microsoft.Extensions.Logging;
using RabbitX.Interfaces;
using RabbitX.Models;
using Spectre.Console;

namespace RabbitX.Samples.Common.Handlers;

/// <summary>
/// Dead letter message handler - Processes messages from dead letter queue.
/// Demonstrates how to handle failed messages for manual review or reprocessing.
/// </summary>
public sealed class DeadLetterMessageHandler : IMessageHandler<DeadLetterMessage>
{
    private readonly ILogger<DeadLetterMessageHandler> _logger;
    private static int _processedCount;

    public DeadLetterMessageHandler(ILogger<DeadLetterMessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<DeadLetterMessage> context,
        CancellationToken cancellationToken = default)
    {
        var message = context.Message;
        var count = Interlocked.Increment(ref _processedCount);

        AnsiConsole.MarkupLine(
            $"[yellow][[DLQ #{count}]][/] Processing dead letter message");

        // Log headers for debugging
        if (context.Headers.Count > 0)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Header")
                .AddColumn("Value");

            foreach (var header in context.Headers)
            {
                table.AddRow(
                    $"[dim]{header.Key}[/]",
                    $"[cyan]{header.Value}[/]");
            }

            AnsiConsole.Write(table);
        }

        // Simulate analysis/storage
        await Task.Delay(100, cancellationToken);

        _logger.LogWarning(
            "Dead letter message processed. OriginalMessageId: {MessageId}, " +
            "FailureReason: {Reason}, OriginalQueue: {Queue}",
            message.OriginalMessageId,
            message.FailureReason,
            message.OriginalQueue);

        AnsiConsole.MarkupLine(
            $"[yellow][[DLQ #{count}]][/] [bold green]STORED[/] - " +
            $"Message logged for manual review");

        return ConsumeResult.Ack;
    }
}

/// <summary>
/// Generic wrapper for dead letter messages.
/// </summary>
public sealed class DeadLetterMessage
{
    public string OriginalMessageId { get; set; } = string.Empty;
    public string OriginalQueue { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string OriginalPayload { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
}
