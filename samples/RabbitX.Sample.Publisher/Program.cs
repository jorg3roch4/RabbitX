using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitX.Samples.Common.Configuration;
using RabbitX.Samples.Common.Demo;
using RabbitX.Samples.Common.Extensions;
using Spectre.Console;

// Show usage if help requested
if (args.Contains("--help") || args.Contains("-h"))
{
    ConfigurationSelector.ShowPublisherUsage();
    return;
}

// Select configuration
var config = ConfigurationSelector.SelectPublisherConfiguration(args);

// Display selected configuration
ConfigurationSelector.DisplayConfiguration(config, isPublisher: true);

// Build host
var builder = Host.CreateApplicationBuilder(args);

// Register sample configuration
builder.Services.AddSingleton(config);

// Configure RabbitX
builder.Services.AddRabbitXPublisher(builder.Configuration, config);

// Register demo runner
builder.Services.AddTransient<PublisherDemoRunner>();

var host = builder.Build();

// Run demo
using var scope = host.Services.CreateScope();
var demoRunner = scope.ServiceProvider.GetRequiredService<PublisherDemoRunner>();

try
{
    await demoRunner.RunAsync();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    Environment.ExitCode = 1;
}

if (!config.NonInteractive)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Press any key to exit...[/]");
    Console.ReadKey(true);
}
