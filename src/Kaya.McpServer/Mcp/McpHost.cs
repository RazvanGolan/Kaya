using System.Reflection;
using Kaya.McpServer.Configuration;
using Kaya.McpServer.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kaya.McpServer.Mcp;

public static class McpHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // MCP stdio transport requires clean JSON-RPC frames on stdio.
        // Disable default console logging to avoid protocol parse warnings in clients.
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.None);

        builder.Services.AddSingleton(McpServerOptions.FromArgsAndEnv(args));
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<HttpInvocationService>();
        builder.Services.AddSingleton<GrpcInvocationService>();
        builder.Services.AddSingleton<SignalRInvocationService>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(Assembly.GetExecutingAssembly());

        using var host = builder.Build();
        await host.RunAsync();
    }
}
