using System.Reflection;
using Kaya.McpServer.Configuration;
using Kaya.McpServer.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaya.McpServer.Mcp;

public static class McpHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(McpServerOptions.FromArgsAndEnv(args));
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<HttpInvocationService>();
        builder.Services.AddSingleton<GrpcInvocationService>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(Assembly.GetExecutingAssembly());

        using var host = builder.Build();
        await host.RunAsync();
    }
}
