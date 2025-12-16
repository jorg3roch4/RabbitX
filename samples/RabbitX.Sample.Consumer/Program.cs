using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitX.Samples.Common.Configuration;
using RabbitX.Samples.Common.Demo;
using RabbitX.Samples.Common.Extensions;
using Spectre.Console;

// Show usage if help requested
if (args.Contains("--help") || args.Contains("-h"))
{
    ConfigurationSelector.ShowConsumerUsage();
    return;
}

// Select configuration
var config = ConfigurationSelector.SelectConsumerConfiguration(args);

// Display selected configuration
ConfigurationSelector.DisplayConfiguration(config, isPublisher: false);

// Confirm before starting (skip in non-interactive mode)
if (!config.NonInteractive && !AnsiConsole.Confirm("[yellow]Start consumers with this configuration?[/]", true))
{
    AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
    return;
}

// Build host
var builder = Host.CreateApplicationBuilder(args);

// Register sample configuration
builder.Services.AddSingleton(config);

// Configure RabbitX
builder.Services.AddRabbitXConsumer(builder.Configuration, config);

// Register demo info service
builder.Services.AddHostedService<ConsumerDemoRunner>();

var host = builder.Build();

AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[cyan]Starting RabbitX Consumer Host[/]"));

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
}
