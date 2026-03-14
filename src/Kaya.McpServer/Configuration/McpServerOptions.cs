using System.Text.Json;

namespace Kaya.McpServer.Configuration;

public sealed class McpServerOptions
{
    public string ApiBaseUrl { get; }
    public string GrpcProxyBaseUrl { get; }

    public McpServerOptions(string? apiBaseUrl = null, string? grpcProxyBaseUrl = null)
    {
        ApiBaseUrl = NormalizeBaseUrl(apiBaseUrl
            ?? Environment.GetEnvironmentVariable("KAYA_API_BASE_URL")
            ?? "http://localhost:5000");

        GrpcProxyBaseUrl = NormalizeBaseUrl(grpcProxyBaseUrl
            ?? Environment.GetEnvironmentVariable("KAYA_GRPC_PROXY_BASE_URL")
            ?? "http://localhost:5000");
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

        return new McpServerOptions(apiBaseUrl, grpcProxyBaseUrl);
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

    private sealed class McpServerFileConfig
    {
        public string? ApiBaseUrl { get; init; }
        public string? GrpcProxyBaseUrl { get; init; }
    }
}
