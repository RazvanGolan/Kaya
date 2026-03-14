using System.Text.Json;
using System.Net.Http;

namespace Kaya.McpServer.Configuration;

public sealed class McpServerOptions
{
    public string ApiBaseUrl { get; }
    public string GrpcProxyBaseUrl { get; }
    public string SignalRDebugRoutePrefix { get; }

    public McpServerOptions(
        string? apiBaseUrl = null,
        string? grpcProxyBaseUrl = null,
        string? signalRDebugRoutePrefix = null)
    {
        ApiBaseUrl = NormalizeBaseUrl(apiBaseUrl
            ?? Environment.GetEnvironmentVariable("KAYA_API_BASE_URL")
            ?? "http://localhost:5000");

        GrpcProxyBaseUrl = NormalizeBaseUrl(grpcProxyBaseUrl
            ?? Environment.GetEnvironmentVariable("KAYA_GRPC_PROXY_BASE_URL")
            ?? ApiBaseUrl);

        SignalRDebugRoutePrefix = NormalizePath(signalRDebugRoutePrefix
            ?? Environment.GetEnvironmentVariable("KAYA_SIGNALR_DEBUG_ROUTE")
            ?? "/kaya-signalr");
    }

    public static McpServerOptions FromArgsAndEnv(string[] args)
    {
        var configPath = TryGetArgValue(args, "--config")
                         ?? Environment.GetEnvironmentVariable("KAYA_MCP_CONFIG");

        McpServerFileConfig? fileConfig = null;
        if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                fileConfig = JsonSerializer.Deserialize<McpServerFileConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                // Ignore invalid config content and fall back to args/env/defaults.
            }
        }

        var apiBaseUrl = TryGetArgValue(args, "--api-url")
                         ?? fileConfig?.ApiBaseUrl;

        var grpcProxyBaseUrl = TryGetArgValue(args, "--grpc-proxy-url")
                               ?? fileConfig?.GrpcProxyBaseUrl;

        var signalRDebugRoutePrefix = TryGetArgValue(args, "--signalr-debug-route")
                                      ?? fileConfig?.SignalRDebugRoutePrefix;

        return new McpServerOptions(apiBaseUrl, grpcProxyBaseUrl, signalRDebugRoutePrefix);
    }

    private static string? TryGetArgValue(IReadOnlyList<string> args, string key)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Count)
                {
                    return args[i + 1];
                }

                continue;
            }

            if (arg.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(key.Length + 1)..];
            }
        }

        return null;
    }

    public static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "http://localhost:5000";
        }

        return baseUrl.Trim().TrimEnd('/');
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.TrimEnd('/');
    }

    private sealed class McpServerFileConfig
    {
        public string? ApiBaseUrl { get; init; }
        public string? GrpcProxyBaseUrl { get; init; }
        public string? SignalRDebugRoutePrefix { get; init; }
    }
}
