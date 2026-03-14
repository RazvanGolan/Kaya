using System.ComponentModel;
using System.Text.Json;
using Kaya.McpServer.Core;
using KayaOptions = Kaya.McpServer.Configuration.McpServerOptions;
using ModelContextProtocol.Server;

namespace Kaya.McpServer.Mcp.Tools;

[McpServerToolType]
public sealed class InvocationTools(
    KayaOptions options,
    HttpInvocationService httpInvocationService,
    GrpcInvocationService grpcInvocationService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "http_invoke", Title = "HTTP Invoke")]
    [Description("Invokes an HTTP endpoint on the running application. Returns status code, response body, content type, and elapsed time.")]
    public async Task<string> HttpInvoke(
        [Description("HTTP method: GET, POST, PUT, DELETE, PATCH")] string method,
        [Description("Path and query string, for example /api/orders/1")] string path,
        [Description("Optional JSON request body")] string? body = null,
        [Description("Optional JSON object string of extra headers")] string? headers = null,
        [Description("Optional base URL override")] string? baseUrl = null)
    {
        try
        {
            var parsedHeaders = ParseJsonMap(headers);
            var result = await httpInvocationService.InvokeAsync(
                method,
                path,
                parsedHeaders,
                body,
                baseUrl ?? options.ApiBaseUrl);

            return JsonSerializer.Serialize(new
            {
                statusCode = result.StatusCode,
                body = result.Body,
                contentType = result.ContentType,
                elapsedMs = result.ElapsedMs
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "grpc_invoke", Title = "gRPC Invoke")]
    [Description("Invokes a unary gRPC method via Kaya.GrpcExplorer middleware proxy.")]
    public async Task<string> GrpcInvoke(
        [Description("gRPC server address, e.g. localhost:5001")] string serverAddress,
        [Description("Fully-qualified service name, e.g. orders.OrderService")] string serviceName,
        [Description("Method name")] string methodName,
        [Description("Request JSON payload")] string requestJson,
        [Description("Optional JSON object of metadata headers")] string? metadata = null,
        [Description("Optional gRPC proxy base URL override")] string? grpcProxyBaseUrl = null)
    {
        try
        {
            var parsedMetadata = ParseJsonMap(metadata);
            return await grpcInvocationService.InvokeAsync(
                serverAddress,
                serviceName,
                methodName,
                requestJson,
                parsedMetadata,
                grpcProxyBaseUrl ?? options.GrpcProxyBaseUrl);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "grpc_stream_start", Title = "gRPC Stream Start")]
    [Description("Starts a gRPC streaming session and returns a sessionId.")]
    public async Task<string> GrpcStreamStart(
        [Description("gRPC server address")] string serverAddress,
        [Description("Fully-qualified service name")] string serviceName,
        [Description("Method name")] string methodName,
        [Description("Initial JSON payload")] string initialMessageJson,
        [Description("Optional JSON object of metadata headers")] string? metadata = null,
        [Description("Optional gRPC proxy base URL override")] string? grpcProxyBaseUrl = null)
    {
        try
        {
            var parsedMetadata = ParseJsonMap(metadata);
            return await grpcInvocationService.StreamStartAsync(
                serverAddress,
                serviceName,
                methodName,
                initialMessageJson,
                parsedMetadata,
                grpcProxyBaseUrl ?? options.GrpcProxyBaseUrl);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "grpc_stream_send", Title = "gRPC Stream Send")]
    [Description("Sends one message into an active streaming session.")]
    public async Task<string> GrpcStreamSend(
        [Description("Session ID returned by grpc_stream_start")] string sessionId,
        [Description("Message JSON payload")] string messageJson,
        [Description("Optional gRPC proxy base URL override")] string? grpcProxyBaseUrl = null)
    {
        try
        {
            await grpcInvocationService.StreamSendAsync(
                sessionId,
                messageJson,
                grpcProxyBaseUrl ?? options.GrpcProxyBaseUrl);
            return "ok";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "grpc_stream_events", Title = "gRPC Stream Events")]
    [Description("Reads buffered server responses from an active streaming session.")]
    public async Task<string> GrpcStreamEvents(
        [Description("Session ID returned by grpc_stream_start")] string sessionId,
        [Description("How long to collect events before returning")] int durationSeconds = 5,
        [Description("Optional gRPC proxy base URL override")] string? grpcProxyBaseUrl = null)
    {
        try
        {
            var events = await grpcInvocationService.DrainStreamEventsAsync(
                sessionId,
                durationSeconds,
                grpcProxyBaseUrl ?? options.GrpcProxyBaseUrl);
            return JsonSerializer.Serialize(events, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "grpc_stream_end", Title = "gRPC Stream End")]
    [Description("Completes the client-side of a streaming session.")]
    public async Task<string> GrpcStreamEnd(
        [Description("Session ID returned by grpc_stream_start")] string sessionId,
        [Description("Optional gRPC proxy base URL override")] string? grpcProxyBaseUrl = null)
    {
        try
        {
            await grpcInvocationService.StreamEndAsync(
                sessionId,
                grpcProxyBaseUrl ?? options.GrpcProxyBaseUrl);
            return "ok";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static Dictionary<string, string>? ParseJsonMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
        return map is { Count: > 0 } ? map : null;
    }
}
