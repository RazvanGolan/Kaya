using Kaya.McpServer.Configuration;

namespace Kaya.McpServer.Tests;

public sealed class McpServerOptionsTests
{
    [Fact]
    public void NormalizeBaseUrl_ShouldTrimAndDropTrailingSlash()
    {
        var result = McpServerOptions.NormalizeBaseUrl("  http://localhost:5121/ ");

        Assert.Equal("http://localhost:5121", result);
    }

    [Fact]
    public void NormalizePath_ShouldAddLeadingSlashAndTrimTrailingSlash()
    {
        var result = McpServerOptions.NormalizePath(" kaya-signalr/ ");

        Assert.Equal("/kaya-signalr", result);
    }

    [Fact]
    public void FromArgsAndEnv_ShouldUseFileConfigValues()
    {
        var configPath = CreateTempConfigFile("""
        {
          "apiBaseUrl": "http://file-api:7000/",
          "grpcProxyBaseUrl": "http://file-grpc:7001/",
          "signalRDebugRoutePrefix": "signalr-dev/"
        }
        """);

        try
        {
            using var apiEnv = new EnvVarScope("KAYA_API_BASE_URL", null);
            using var grpcEnv = new EnvVarScope("KAYA_GRPC_PROXY_BASE_URL", null);
            using var routeEnv = new EnvVarScope("KAYA_SIGNALR_DEBUG_ROUTE", null);

            var options = McpServerOptions.FromArgsAndEnv(["--config", configPath]);

            Assert.Equal("http://file-api:7000", options.ApiBaseUrl);
            Assert.Equal("http://file-grpc:7001", options.GrpcProxyBaseUrl);
            Assert.Equal("/signalr-dev", options.SignalRDebugRoutePrefix);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public void FromArgsAndEnv_ShouldPreferArgsOverFileConfig()
    {
        var configPath = CreateTempConfigFile("""
        {
          "apiBaseUrl": "http://file-api:7000/",
          "grpcProxyBaseUrl": "http://file-grpc:7001/",
          "signalRDebugRoutePrefix": "/signalr-file"
        }
        """);

        try
        {
            string[] args =
            [
                "--config", configPath,
                "--api-url", "http://arg-api:9000/",
                "--grpc-proxy-url=http://arg-grpc:9001/",
                "--signalr-debug-route", "signalr-arg/"
            ];

            var options = McpServerOptions.FromArgsAndEnv(args);

            Assert.Equal("http://arg-api:9000", options.ApiBaseUrl);
            Assert.Equal("http://arg-grpc:9001", options.GrpcProxyBaseUrl);
            Assert.Equal("/signalr-arg", options.SignalRDebugRoutePrefix);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public void FromArgsAndEnv_ShouldIgnoreInvalidConfigAndUseDefaults()
    {
        var configPath = CreateTempConfigFile("{ invalid json }");

        try
        {
            using var apiEnv = new EnvVarScope("KAYA_API_BASE_URL", null);
            using var grpcEnv = new EnvVarScope("KAYA_GRPC_PROXY_BASE_URL", null);
            using var routeEnv = new EnvVarScope("KAYA_SIGNALR_DEBUG_ROUTE", null);

            var options = McpServerOptions.FromArgsAndEnv(["--config", configPath]);

            Assert.Equal("http://localhost:5000", options.ApiBaseUrl);
            Assert.Equal("http://localhost:5000", options.GrpcProxyBaseUrl);
            Assert.Equal("/kaya-signalr", options.SignalRDebugRoutePrefix);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    private static string CreateTempConfigFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaya-mcp-options-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, contents);
        return path;
    }

    private sealed class EnvVarScope : IDisposable
    {
        private readonly string _key;
        private readonly string? _originalValue;

        public EnvVarScope(string key, string? value)
        {
            _key = key;
            _originalValue = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(_key, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_key, _originalValue);
        }
    }
}
