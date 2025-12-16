using Microsoft.Extensions.Logging;
using RabbitX.Interfaces;
using RabbitX.Models;
using RabbitX.Samples.Common.Configuration;
using RabbitX.Samples.Common.Messages;
using Spectre.Console;

namespace RabbitX.Samples.Common.Demo;

/// <summary>
/// Runs publisher demo scenarios.
/// </summary>
public sealed class PublisherDemoRunner
{
    private readonly IPublisherFactory _publisherFactory;
    private readonly ILogger<PublisherDemoRunner> _logger;
    private readonly SampleConfiguration _config;

    public PublisherDemoRunner(
        IPublisherFactory publisherFactory,
        ILogger<PublisherDemoRunner> logger,
        SampleConfiguration config)
    {
        _publisherFactory = publisherFactory;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Runs the demo based on configuration.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        AnsiConsole.Write(new Rule("[green]Starting Publisher Demo[/]"));
        AnsiConsole.WriteLine();

        switch (_config.ConsumerScenario)
        {
            case ConsumerScenario.Single:
                await RunSinglePublisherDemo(cancellationToken);
                break;

            case ConsumerScenario.Multiple:
                await RunMultiplePublishersDemo(cancellationToken);
                break;

            default:
                await RunSinglePublisherDemo(cancellationToken);
                break;
        }
    }

    private async Task RunSinglePublisherDemo(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[cyan]Scenario: Single Publisher (Tasks)[/]");
        AnsiConsole.WriteLine();

        var publisher = _publisherFactory.CreatePublisher<TaskMessage>("TaskPublisher");

        var messageCount = _config.MessageCount > 0
            ? _config.MessageCount
            : AnsiConsole.Prompt(
                new TextPrompt<int>("[yellow]How many messages to publish?[/]")
                    .DefaultValue(5)
                    .ValidationErrorMessage("[red]Please enter a valid number[/]"));

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Publishing tasks[/]", maxValue: messageCount);

                for (int i = 1; i <= messageCount; i++)
                {
                    var message = TaskMessage.CreateSample(i);

                    if (_config.PublisherMode == PublisherMode.Reliable &&
                        publisher is IReliableMessagePublisher<TaskMessage> reliablePublisher)
                    {
                        var result = await reliablePublisher.PublishWithConfirmAsync(
                            message,
                            new PublishOptions { CorrelationId = message.TaskId },
                            cancellationToken);

                        LogPublishResult(message.TaskId, result);
                    }
                    else
                    {
                        await publisher.PublishAsync(message, cancellationToken);
                        AnsiConsole.MarkupLine(
                            $"[green]Published[/] Task [yellow]{message.TaskId}[/]");
                    }

                    task.Increment(1);
                    await Task.Delay(100, cancellationToken);
                }
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Completed! Published {messageCount} task messages.[/]");
    }

    private async Task RunMultiplePublishersDemo(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[cyan]Scenario: Multiple Publishers (Tasks + Emails + Reports)[/]");
        AnsiConsole.WriteLine();

        var taskPublisher = _publisherFactory.CreatePublisher<TaskMessage>("TaskPublisher");
        var emailPublisher = _publisherFactory.CreatePublisher<EmailMessage>("EmailPublisher");
        var reportPublisher = _publisherFactory.CreatePublisher<ReportMessage>("ReportPublisher");

        var messageCount = _config.MessageCount > 0
            ? _config.MessageCount
            : AnsiConsole.Prompt(
                new TextPrompt<int>("[yellow]How many messages per type to publish?[/]")
                    .DefaultValue(3)
                    .ValidationErrorMessage("[red]Please enter a valid number[/]"));

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var taskTask = ctx.AddTask("[green]Publishing tasks[/]", maxValue: messageCount);
                var emailTask = ctx.AddTask("[blue]Publishing emails[/]", maxValue: messageCount);
                var reportTask = ctx.AddTask("[red]Publishing reports[/]", maxValue: messageCount);

                var tasks = new List<Task>();

                // Publish tasks
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 1; i <= messageCount; i++)
                    {
                        var message = TaskMessage.CreateSample(i);
                        await PublishWithMode(taskPublisher, message, message.TaskId, cancellationToken);
                        taskTask.Increment(1);
                        await Task.Delay(150, cancellationToken);
                    }
                }, cancellationToken));

                // Publish emails
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 1; i <= messageCount; i++)
                    {
                        var message = EmailMessage.CreateSample(i);
                        await PublishWithMode(emailPublisher, message, message.EmailId, cancellationToken);
                        emailTask.Increment(1);
                        await Task.Delay(150, cancellationToken);
                    }
                }, cancellationToken));

                // Publish reports
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 1; i <= messageCount; i++)
                    {
                        var message = ReportMessage.CreateSample(i);
                        await PublishWithMode(reportPublisher, message, message.ReportId, cancellationToken);
                        reportTask.Increment(1);
                        await Task.Delay(150, cancellationToken);
                    }
                }, cancellationToken));

                await Task.WhenAll(tasks);
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Completed! Published {messageCount * 3} total messages.[/]");
    }

    private async Task PublishWithMode<T>(
        IMessagePublisher<T> publisher,
        T message,
        string messageId,
        CancellationToken cancellationToken) where T : class
    {
        if (_config.PublisherMode == PublisherMode.Reliable &&
            publisher is IReliableMessagePublisher<T> reliablePublisher)
        {
            var result = await reliablePublisher.PublishWithConfirmAsync(
                message,
                new PublishOptions { CorrelationId = messageId },
                cancellationToken);

            LogPublishResult(messageId, result);
        }
        else
        {
            await publisher.PublishAsync(message, cancellationToken);
            _logger.LogDebug("Published message {MessageId}", messageId);
        }
    }

    private void LogPublishResult(string messageId, PublishResult result)
    {
        if (result.Success)
        {
            _logger.LogDebug(
                "Message {MessageId} confirmed by broker",
                messageId);
        }
        else
        {
            _logger.LogWarning(
                "Message {MessageId} publish failed: {Error}",
                messageId,
                result.ErrorMessage);
        }
    }
}
