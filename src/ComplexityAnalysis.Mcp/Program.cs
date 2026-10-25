using ComplexityAnalysis.Mcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

if (!McpHostOptions.TryParse(args, out McpHostOptions? options, out string? error)
    || options is null)
{
    await Console.Error.WriteLineAsync(error ?? "Invalid MCP host configuration.");
    Environment.ExitCode = 2;
    return;
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new ComplexityMcpService(options));
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ComplexityTools>();

await builder.Build().RunAsync();
