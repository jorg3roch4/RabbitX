using Microsoft.Extensions.Logging;
using RabbitX.Interfaces;
using RabbitX.Models;
using RabbitX.Samples.Common.Messages;
using Spectre.Console;

namespace RabbitX.Samples.Common.Handlers;

/// <summary>
/// Task message handler - Always succeeds.
/// Demonstrates successful message processing flow.
/// </summary>
public sealed class TaskMessageHandler : IMessageHandler<TaskMessage>
{
    private readonly ILogger<TaskMessageHandler> _logger;
    private static int _processedCount;

    public TaskMessageHandler(ILogger<TaskMessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ConsumeResult> HandleAsync(
        MessageContext<TaskMessage> context,
        CancellationToken cancellationToken = default)
    {
        var message = context.Message;
        var count = Interlocked.Increment(ref _processedCount);

        AnsiConsole.MarkupLine(
            $"[green][[TASK #{count}]][/] Processing [yellow]{message.TaskId}[/] " +
            $"Type: [cyan]{message.TaskType}[/] Priority: [cyan]{message.Priority}[/]");

        // Simulate processing time
        await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} completed successfully. Type: {TaskType}",
            message.TaskId,
            message.TaskType);

        AnsiConsole.MarkupLine(
            $"[green][[TASK #{count}]][/] [bold green]SUCCESS[/] - " +
            $"Task [yellow]{message.TaskId}[/] completed");

        return ConsumeResult.Ack;
    }
}
