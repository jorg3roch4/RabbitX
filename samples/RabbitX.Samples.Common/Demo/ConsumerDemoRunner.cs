using Microsoft.Extensions.Hosting;
using RabbitX.Samples.Common.Configuration;
using Spectre.Console;

namespace RabbitX.Samples.Common.Demo;

/// <summary>
/// Runs consumer demo scenarios.
/// </summary>
public sealed class ConsumerDemoRunner : BackgroundService
{
    private readonly SampleConfiguration _config;
    private readonly IHostApplicationLifetime _lifetime;

    public ConsumerDemoRunner(SampleConfiguration config, IHostApplicationLifetime lifetime)
    {
        _config = config;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give consumers time to start
        await Task.Delay(1000, stoppingToken);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Consumers Running[/]"));
        AnsiConsole.WriteLine();

        ShowActiveConsumers();
        ShowRetryConfiguration();

        if (_config.NonInteractive && _config.MessageCount > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Running for {_config.MessageCount} seconds (non-interactive mode)...[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop...[/]");
        }
        AnsiConsole.WriteLine();

        // Keep running until cancelled or duration expires
        try
        {
            if (_config.NonInteractive && _config.MessageCount > 0)
            {
                // Run for specified duration then stop
                await Task.Delay(TimeSpan.FromSeconds(_config.MessageCount), stoppingToken);
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[green]Duration completed, shutting down...[/]"));
                _lifetime.StopApplication();
            }
            else
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[yellow]Shutting down consumers...[/]"));
        }
    }

    private void ShowActiveConsumers()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[cyan]Consumer[/]")
            .AddColumn("[yellow]Queue[/]")
            .AddColumn("[green]Handler Behavior[/]");

        switch (_config.ConsumerScenario)
        {
            case ConsumerScenario.Single:
                table.AddRow("TaskConsumer", "tasks.queue", "Always succeeds");
                break;

            case ConsumerScenario.Multiple:
                table.AddRow("TaskConsumer", "tasks.queue", "Always succeeds");
                table.AddRow("EmailConsumer", "emails.queue", "Fails randomly (50%)");
                table.AddRow("ReportConsumer", "reports.queue", "Always fails (DLX demo)");
                break;

            case ConsumerScenario.DeadLetterProcessor:
                table.AddRow("DeadLetterConsumer", "*.dlq", "Processes failed messages");
                break;
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private void ShowRetryConfiguration()
    {
        var panel = new Panel(GetRetryDescription())
            .Header("[cyan]Retry Configuration[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private string GetRetryDescription()
    {
        var lines = new List<string>();

        lines.Add($"[yellow]Strategy:[/] {_config.RetryStrategy}");

        switch (_config.RetryStrategy)
        {
            case RetryStrategyType.SpecificDelays:
                lines.Add("[dim]Delays: 10s -> 30s -> 60s -> 120s -> 300s[/]");
                lines.Add("[dim]Max Retries: 5[/]");
                break;

            case RetryStrategyType.ExponentialBackoff:
                lines.Add("[dim]Initial: 5s, Multiplier: 2x, Max: 300s[/]");
                lines.Add("[dim]Jitter: Enabled[/]");
                lines.Add("[dim]Max Retries: 5[/]");
                break;

            case RetryStrategyType.NoRetry:
                lines.Add("[dim]Messages fail immediately[/]");
                break;
        }

        lines.Add("");
        lines.Add($"[yellow]On Exhausted:[/] {_config.OnRetryExhausted}");

        switch (_config.OnRetryExhausted)
        {
            case RetryExhaustedActionType.SendToDeadLetter:
                lines.Add("[dim]Failed messages go to DLX for later analysis[/]");
                break;

            case RetryExhaustedActionType.Requeue:
                lines.Add("[dim]Messages return to original queue[/]");
                break;

            case RetryExhaustedActionType.Discard:
                lines.Add("[dim]Messages are acknowledged and lost[/]");
                break;
        }

        return string.Join("\n", lines);
    }
}
