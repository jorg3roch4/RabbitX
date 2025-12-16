using Microsoft.Extensions.Logging;
using RabbitX.Interfaces;
using RabbitX.Models;
using RabbitX.Samples.Common.Messages;
using Spectre.Console;

namespace RabbitX.Samples.Common.Handlers;

/// <summary>
/// Report message handler - Always fails.
/// Demonstrates dead letter exchange behavior when retries are exhausted.
/// Simulates a scenario where the report service is down.
/// </summary>
public sealed class ReportMessageHandler : IMessageHandler<ReportMessage>
{
    private readonly ILogger<ReportMessageHandler> _logger;
    private static int _processedCount;

    public ReportMessageHandler(ILogger<ReportMessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<ReportMessage> context,
        CancellationToken cancellationToken = default)
    {
        var message = context.Message;
        var count = Interlocked.Increment(ref _processedCount);
        var retryInfo = context.RetryCount > 0 ? $" (Retry #{context.RetryCount})" : "";

        AnsiConsole.MarkupLine(
            $"[red][[REPORT #{count}]][/] Generating [yellow]{message.ReportId}[/] " +
            $"Type: [cyan]{message.ReportType}[/] Format: [cyan]{message.Format}[/]{retryInfo}");

        // Simulate processing time
        await Task.Delay(Random.Shared.Next(200, 400), cancellationToken);

        // Always fail - simulates report service being unavailable
        _logger.LogError(
            "Report {ReportId} generation failed (attempt {Attempt}). " +
            "Report service unavailable - demonstrating DLX behavior.",
            message.ReportId,
            context.RetryCount + 1);

        AnsiConsole.MarkupLine(
            $"[red][[REPORT #{count}]][/] [bold red]FAILED[/] - " +
            $"Report service unavailable for [yellow]{message.ReportId}[/] " +
            $"[dim](Will go to DLX after max retries)[/]");

        return ConsumeResult.Retry;
    }
}
