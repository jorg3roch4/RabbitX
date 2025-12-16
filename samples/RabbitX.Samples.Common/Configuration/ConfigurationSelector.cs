using Spectre.Console;

namespace RabbitX.Samples.Common.Configuration;

/// <summary>
/// Interactive configuration selector using Spectre.Console.
/// </summary>
public static class ConfigurationSelector
{
    /// <summary>
    /// Selects publisher configuration interactively or from CLI arguments.
    /// </summary>
    public static SampleConfiguration SelectPublisherConfiguration(string[] args)
    {
        if (args.Length >= 3)
            return ParsePublisherArgs(args);

        return SelectPublisherInteractive();
    }

    /// <summary>
    /// Selects consumer configuration interactively or from CLI arguments.
    /// </summary>
    public static SampleConfiguration SelectConsumerConfiguration(string[] args)
    {
        if (args.Length >= 3)
            return ParseConsumerArgs(args);

        return SelectConsumerInteractive();
    }

    private static SampleConfiguration ParsePublisherArgs(string[] args)
    {
        var config = new SampleConfiguration
        {
            ConfigMode = ParseConfigMode(args[0]),
            PublisherMode = ParsePublisherMode(args[1]),
            ConsumerScenario = args.Length > 2 && args[2] == "multiple"
                ? ConsumerScenario.Multiple
                : ConsumerScenario.Single,
            NonInteractive = true
        };

        // Check for message count argument (--count=N or -n N)
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--count="))
            {
                if (int.TryParse(args[i][8..], out var count))
                    config.MessageCount = count;
            }
            else if ((args[i] == "-n" || args[i] == "--count") && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var count))
                    config.MessageCount = count;
            }
        }

        return config;
    }

    private static SampleConfiguration ParseConsumerArgs(string[] args)
    {
        var config = new SampleConfiguration
        {
            ConfigMode = ParseConfigMode(args[0]),
            RetryStrategy = ParseRetryStrategy(args[1]),
            OnRetryExhausted = ParseRetryExhaustedAction(args[2]),
            NonInteractive = true
        };

        if (args.Length > 3)
        {
            config.ConsumerScenario = args[3] switch
            {
                "single" => ConsumerScenario.Single,
                "multiple" => ConsumerScenario.Multiple,
                "dlq" => ConsumerScenario.DeadLetterProcessor,
                _ => ConsumerScenario.Multiple
            };
        }

        // Check for duration argument (--duration=N seconds)
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--duration="))
            {
                if (int.TryParse(args[i][11..], out var duration))
                    config.MessageCount = duration; // Reuse as duration in seconds for consumer
            }
            else if ((args[i] == "-d" || args[i] == "--duration") && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var duration))
                    config.MessageCount = duration;
            }
        }

        return config;
    }

    private static SampleConfiguration SelectPublisherInteractive()
    {
        ShowHeader("RabbitX Publisher Sample");

        var configMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select Configuration Mode:[/]")
                .AddChoices("FluentApi (code-based)", "AppSettings (appsettings.json)"));

        var publisherMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select Publisher Mode:[/]")
                .AddChoices("Reliable (with confirms)", "Basic (fire-and-forget)"));

        var scenario = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select Scenario:[/]")
                .AddChoices(
                    "Single Publisher (payments only)",
                    "Multiple Publishers (payments + notifications + orders)"));

        return new SampleConfiguration
        {
            ConfigMode = configMode.Contains("FluentApi")
                ? ConfigurationMode.FluentApi
                : ConfigurationMode.AppSettings,
            PublisherMode = publisherMode.Contains("Reliable")
                ? PublisherMode.Reliable
                : PublisherMode.Basic,
            ConsumerScenario = scenario.Contains("Single")
                ? ConsumerScenario.Single
                : ConsumerScenario.Multiple
        };
    }

    private static SampleConfiguration SelectConsumerInteractive()
    {
        ShowHeader("RabbitX Consumer Sample");

        var configMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select Configuration Mode:[/]")
                .AddChoices("FluentApi (code-based)", "AppSettings (appsettings.json)"));

        var retryStrategy = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select Retry Strategy:[/]")
                .AddChoices(
                    "Specific Delays (10s, 30s, 60s, 120s, 300s)",
                    "Exponential Backoff (5s * 2^n, max 5min)",
                    "No Retry"));

        var onExhausted = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select Action When Retries Exhausted:[/]")
                .AddChoices(
                    "Send to Dead Letter Exchange",
                    "Requeue to Original Queue",
                    "Discard Message"));

        var scenario = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select Consumer Scenario:[/]")
                .AddChoices(
                    "Single Consumer (payments only)",
                    "Multiple Consumers (payments + notifications + orders)",
                    "Dead Letter Processor (process failed messages)"));

        return new SampleConfiguration
        {
            ConfigMode = configMode.Contains("FluentApi")
                ? ConfigurationMode.FluentApi
                : ConfigurationMode.AppSettings,
            RetryStrategy = retryStrategy switch
            {
                var s when s.Contains("Specific") => RetryStrategyType.SpecificDelays,
                var s when s.Contains("Exponential") => RetryStrategyType.ExponentialBackoff,
                _ => RetryStrategyType.NoRetry
            },
            OnRetryExhausted = onExhausted switch
            {
                var s when s.Contains("Dead Letter") => RetryExhaustedActionType.SendToDeadLetter,
                var s when s.Contains("Requeue") => RetryExhaustedActionType.Requeue,
                _ => RetryExhaustedActionType.Discard
            },
            ConsumerScenario = scenario switch
            {
                var s when s.Contains("Single") => ConsumerScenario.Single,
                var s when s.Contains("Dead Letter") => ConsumerScenario.DeadLetterProcessor,
                _ => ConsumerScenario.Multiple
            }
        };
    }

    private static void ShowHeader(string title)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText(title)
                .LeftJustified()
                .Color(Color.Cyan1));
        AnsiConsole.WriteLine();
    }

    private static ConfigurationMode ParseConfigMode(string arg) =>
        arg.ToLowerInvariant() switch
        {
            "fluent" or "fluentapi" => ConfigurationMode.FluentApi,
            "appsettings" or "config" => ConfigurationMode.AppSettings,
            _ => ConfigurationMode.FluentApi
        };

    private static RetryStrategyType ParseRetryStrategy(string arg) =>
        arg.ToLowerInvariant() switch
        {
            "specific" or "delays" => RetryStrategyType.SpecificDelays,
            "exponential" or "backoff" => RetryStrategyType.ExponentialBackoff,
            "none" or "noretry" => RetryStrategyType.NoRetry,
            _ => RetryStrategyType.SpecificDelays
        };

    private static RetryExhaustedActionType ParseRetryExhaustedAction(string arg) =>
        arg.ToLowerInvariant() switch
        {
            "deadletter" or "dlx" => RetryExhaustedActionType.SendToDeadLetter,
            "requeue" => RetryExhaustedActionType.Requeue,
            "discard" => RetryExhaustedActionType.Discard,
            _ => RetryExhaustedActionType.SendToDeadLetter
        };

    private static PublisherMode ParsePublisherMode(string arg) =>
        arg.ToLowerInvariant() switch
        {
            "reliable" or "confirms" => PublisherMode.Reliable,
            "basic" or "fire" => PublisherMode.Basic,
            _ => PublisherMode.Reliable
        };

    /// <summary>
    /// Displays the selected configuration.
    /// </summary>
    public static void DisplayConfiguration(SampleConfiguration config, bool isPublisher)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[cyan]Setting[/]")
            .AddColumn("[yellow]Value[/]");

        table.AddRow("Configuration Mode", config.ConfigMode.ToString());

        if (isPublisher)
        {
            table.AddRow("Publisher Mode", config.PublisherMode.ToString());
        }
        else
        {
            table.AddRow("Retry Strategy", config.RetryStrategy.ToString());
            table.AddRow("On Retry Exhausted", config.OnRetryExhausted.ToString());
        }

        table.AddRow("Scenario", config.ConsumerScenario.ToString());

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Selected Configuration[/]"));
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Shows usage help.
    /// </summary>
    public static void ShowPublisherUsage()
    {
        AnsiConsole.MarkupLine("[dim]Usage: dotnet run -- [fluent|appsettings] [reliable|basic] [single|multiple][/]");
        AnsiConsole.MarkupLine("[dim]Example: dotnet run -- fluent reliable multiple[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Shows usage help.
    /// </summary>
    public static void ShowConsumerUsage()
    {
        AnsiConsole.MarkupLine("[dim]Usage: dotnet run -- [fluent|appsettings] [specific|exponential|none] [deadletter|requeue|discard] [single|multiple|dlq][/]");
        AnsiConsole.MarkupLine("[dim]Example: dotnet run -- fluent specific deadletter multiple[/]");
        AnsiConsole.WriteLine();
    }
}
