using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitX.Extensions;
using RabbitX.Rpc;
using RabbitX.Sample.Rpc.Handlers;
using RabbitX.Sample.Rpc.Messages;
using Spectre.Console;

// Display header
AnsiConsole.Write(new FigletText("RabbitX RPC").Color(Color.Cyan1));
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[dim]Demonstrates the Sync-Async-Sync pattern using RabbitMQ RPC[/]");
AnsiConsole.WriteLine();

// Parse arguments
var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "both";
var isServer = mode is "server" or "both";
var isClient = mode is "client" or "both";

if (mode is "help" or "-h" or "--help")
{
    ShowUsage();
    return;
}

// Build the host
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitX(options => options
    .UseConnection("localhost", 5672)
    .UseCredentials("rabbit", "rabbit")
    .UseVirtualHost("/")
    .WithClientName("RabbitX.Sample.Rpc")
    // Calculator RPC: sample.calculator.compute
    .AddRpcClient<CalculateRequest, CalculateResponse>("Calculator", rpc => rpc
        .ToExchange("sample.rpc.exchange", "direct")
        .WithRoutingKey("sample.calculator.compute")
        .WithTimeout(TimeSpan.FromSeconds(30))
        .UseDirectReplyTo())
    .AddRpcHandler<CalculateRequest, CalculateResponse, CalculatorHandler>("CalculatorHandler", handler => handler
        .FromQueue("sample.calculator.compute.queue")
        .BindToExchange("sample.rpc.exchange", "sample.calculator.compute")
        .WithPrefetchCount(10))
    // User Query RPC: sample.users.get-by-id
    .AddRpcClient<GetUserRequest, GetUserResponse>("UserQuery", rpc => rpc
        .ToExchange("sample.rpc.exchange", "direct")
        .WithRoutingKey("sample.users.get-by-id")
        .WithTimeout(TimeSpan.FromSeconds(10))
        .UseDirectReplyTo())
    .AddRpcHandler<GetUserRequest, GetUserResponse, UserQueryHandler>("UserQueryHandler", handler => handler
        .FromQueue("sample.users.get-by-id.queue")
        .BindToExchange("sample.rpc.exchange", "sample.users.get-by-id")
        .WithPrefetchCount(10)));

var host = builder.Build();

// Start the handlers (server mode)
if (isServer)
{
    AnsiConsole.MarkupLine("[green]Starting RPC handlers...[/]");

    var hostedServices = host.Services.GetServices<IHostedService>();
    var rpcService = hostedServices.OfType<RpcConsumerHostedService>().FirstOrDefault();

    if (rpcService != null)
    {
        using var serverCts = new CancellationTokenSource();
        await rpcService.StartAsync(serverCts.Token);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[cyan]Handler[/]")
            .AddColumn("[yellow]Queue[/]")
            .AddRow("CalculatorHandler", "sample.calculator.compute.queue")
            .AddRow("UserQueryHandler", "sample.users.get-by-id.queue");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }
}

// Run client demo
if (isClient)
{
    await Task.Delay(500); // Give handlers time to start

    AnsiConsole.MarkupLine("[green]Starting RPC client demo...[/]");
    AnsiConsole.WriteLine();

    var clientFactory = host.Services.GetRequiredService<IRpcClientFactory>();

    // Calculator demo
    await RunCalculatorDemo(clientFactory);

    // User query demo
    await RunUserQueryDemo(clientFactory);

    // Parallel requests demo
    await RunParallelDemo(clientFactory);

    // Timeout demo
    if (!isServer)
    {
        await RunTimeoutDemo(clientFactory);
    }
}

// If server mode, keep running
if (isServer && !isClient)
{
    AnsiConsole.MarkupLine("[dim]RPC handlers running. Press Ctrl+C to stop...[/]");
    await host.RunAsync();
}
else if (isServer)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Demo complete. Press any key to exit...[/]");
    Console.ReadKey(true);
}

static async Task RunCalculatorDemo(IRpcClientFactory factory)
{
    AnsiConsole.Write(new Rule("[cyan]Calculator RPC Demo[/]"));

    await using var client = factory.CreateClient<CalculateRequest, CalculateResponse>("Calculator");

    var operations = new[]
    {
        (10.0, 5.0, "add"),
        (10.0, 5.0, "subtract"),
        (10.0, 5.0, "multiply"),
        (10.0, 5.0, "divide"),
        (2.0, 10.0, "power"),
        (10.0, 0.0, "divide") // This will return an error
    };

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[cyan]Expression[/]")
        .AddColumn("[green]Result[/]")
        .AddColumn("[yellow]Duration[/]");

    foreach (var (a, b, op) in operations)
    {
        var request = new CalculateRequest { A = a, B = b, Operation = op };

        try
        {
            var result = await client.TryCallAsync(request);

            if (result.Success && string.IsNullOrEmpty(result.Response!.Error))
            {
                table.AddRow(
                    $"{a} {op} {b}",
                    $"[green]{result.Response.Result}[/]",
                    $"{result.Duration.TotalMilliseconds:F0}ms");
            }
            else if (result.Success)
            {
                table.AddRow(
                    $"{a} {op} {b}",
                    $"[red]{result.Response!.Error}[/]",
                    $"{result.Duration.TotalMilliseconds:F0}ms");
            }
            else
            {
                table.AddRow(
                    $"{a} {op} {b}",
                    $"[red]RPC Error: {result.ErrorMessage}[/]",
                    "-");
            }
        }
        catch (Exception ex)
        {
            table.AddRow(
                $"{a} {op} {b}",
                $"[red]{ex.Message}[/]",
                "-");
        }
    }

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

static async Task RunUserQueryDemo(IRpcClientFactory factory)
{
    AnsiConsole.Write(new Rule("[cyan]User Query RPC Demo[/]"));

    await using var client = factory.CreateClient<GetUserRequest, GetUserResponse>("UserQuery");

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[cyan]User ID[/]")
        .AddColumn("[green]Name[/]")
        .AddColumn("[yellow]Email[/]");

    for (int userId = 1; userId <= 4; userId++)
    {
        var response = await client.CallAsync(new GetUserRequest { UserId = userId });

        table.AddRow(
            userId.ToString(),
            response.Name == "Unknown" ? $"[dim]{response.Name}[/]" : response.Name,
            response.Email == "unknown@example.com" ? $"[dim]{response.Email}[/]" : response.Email);
    }

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

static async Task RunParallelDemo(IRpcClientFactory factory)
{
    AnsiConsole.Write(new Rule("[cyan]Parallel RPC Demo[/]"));

    await using var client = factory.CreateClient<CalculateRequest, CalculateResponse>("Calculator");

    AnsiConsole.MarkupLine("[dim]Sending 10 concurrent RPC requests...[/]");

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    var tasks = Enumerable.Range(1, 10)
        .Select(i => client.TryCallAsync(new CalculateRequest
        {
            A = i,
            B = i,
            Operation = "multiply"
        }))
        .ToList();

    var results = await Task.WhenAll(tasks);

    stopwatch.Stop();

    var successCount = results.Count(r => r.Success);
    var avgDuration = results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);

    AnsiConsole.MarkupLine($"[green]Completed {successCount}/10 requests[/]");
    AnsiConsole.MarkupLine($"[yellow]Total time: {stopwatch.ElapsedMilliseconds}ms[/]");
    AnsiConsole.MarkupLine($"[yellow]Average RPC duration: {avgDuration:F0}ms[/]");
    AnsiConsole.WriteLine();
}

static async Task RunTimeoutDemo(IRpcClientFactory factory)
{
    AnsiConsole.Write(new Rule("[cyan]Timeout Demo (No Server)[/]"));

    await using var client = factory.CreateClient<CalculateRequest, CalculateResponse>("Calculator");

    AnsiConsole.MarkupLine("[dim]Sending request with 2s timeout (no server running)...[/]");

    var result = await client.TryCallAsync(
        new CalculateRequest { A = 1, B = 2, Operation = "add" },
        TimeSpan.FromSeconds(2));

    if (result.IsTimeout)
    {
        AnsiConsole.MarkupLine($"[yellow]Request timed out as expected: {result.ErrorMessage}[/]");
    }
    else if (result.Success)
    {
        AnsiConsole.MarkupLine($"[green]Unexpected success: {result.Response!.Result}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine($"[red]Failed: {result.ErrorMessage}[/]");
    }

    AnsiConsole.WriteLine();
}

static void ShowUsage()
{
    AnsiConsole.MarkupLine("[cyan]Usage:[/] dotnet run -- [mode]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[yellow]Modes:[/]");
    AnsiConsole.MarkupLine("  [green]both[/]    - Run both client and server (default)");
    AnsiConsole.MarkupLine("  [green]server[/]  - Run only the RPC handlers");
    AnsiConsole.MarkupLine("  [green]client[/]  - Run only the RPC client demo");
    AnsiConsole.MarkupLine("  [green]help[/]    - Show this help message");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Example:[/]");
    AnsiConsole.MarkupLine("  Terminal 1: dotnet run -- server");
    AnsiConsole.MarkupLine("  Terminal 2: dotnet run -- client");
}
