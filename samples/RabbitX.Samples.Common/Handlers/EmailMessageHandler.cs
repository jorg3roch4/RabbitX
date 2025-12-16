using Microsoft.Extensions.Logging;
using RabbitX.Interfaces;
using RabbitX.Models;
using RabbitX.Samples.Common.Messages;
using Spectre.Console;

namespace RabbitX.Samples.Common.Handlers;

/// <summary>
/// Email message handler - Fails randomly (50% chance).
/// Demonstrates retry behavior simulating external SMTP service failures.
/// </summary>
public sealed class EmailMessageHandler : IMessageHandler<EmailMessage>
{
    private readonly ILogger<EmailMessageHandler> _logger;
    private static int _processedCount;
    private static int _failedCount;

    public EmailMessageHandler(ILogger<EmailMessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<EmailMessage> context,
        CancellationToken cancellationToken = default)
    {
        var message = context.Message;
        var count = Interlocked.Increment(ref _processedCount);
        var retryInfo = context.RetryCount > 0 ? $" (Retry #{context.RetryCount})" : "";

        AnsiConsole.MarkupLine(
            $"[blue][[EMAIL #{count}]][/] Sending to [yellow]{message.To}[/] " +
            $"Subject: [cyan]{message.Subject}[/]{retryInfo}");

        // Simulate SMTP connection time
        await Task.Delay(Random.Shared.Next(100, 300), cancellationToken);

        // 50% chance of failure on first attempts, decreasing with retries
        // Simulates transient SMTP failures that often succeed on retry
        var failureChance = Math.Max(0.1, 0.5 - (context.RetryCount * 0.15));
        var shouldFail = Random.Shared.NextDouble() < failureChance;

        if (shouldFail)
        {
            var failed = Interlocked.Increment(ref _failedCount);

            _logger.LogWarning(
                "Email {EmailId} to {To} failed (attempt {Attempt}). SMTP connection timeout.",
                message.EmailId,
                message.To,
                context.RetryCount + 1);

            AnsiConsole.MarkupLine(
                $"[blue][[EMAIL #{count}]][/] [bold red]FAILED[/] - " +
                $"SMTP timeout for [yellow]{message.EmailId}[/] " +
                $"[dim](Total failures: {failed})[/]");

            return ConsumeResult.Retry;
        }

        _logger.LogInformation(
            "Email {EmailId} sent to {To}. Subject: {Subject}",
            message.EmailId,
            message.To,
            message.Subject);

        AnsiConsole.MarkupLine(
            $"[blue][[EMAIL #{count}]][/] [bold green]SENT[/] - " +
            $"Email [yellow]{message.EmailId}[/] delivered to {message.To}");

        return ConsumeResult.Ack;
    }
}
