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
    GrpcInvocationService grpcInvocationService,
    SignalRInvocationService signalRInvocationService)
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

    [McpServerTool(Name = "signalr_hubs", Title = "SignalR Hubs")]
    [Description("Discovers SignalR hubs from Kaya.ApiExplorer SignalR debug endpoint.")]
    public async Task<string> SignalRHubs(
        [Description("Optional API base URL override")] string? apiBaseUrl = null,
        [Description("Optional SignalR debug route override (default: /kaya-signalr)")] string? signalRDebugRoute = null)
    {
        try
        {
            return await signalRInvocationService.DiscoverHubsAsync(
                apiBaseUrl ?? options.ApiBaseUrl,
                signalRDebugRoute ?? options.SignalRDebugRoutePrefix);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "signalr_connect", Title = "SignalR Connect")]
    [Description("Creates and starts a SignalR hub connection and returns a sessionId.")]
    public async Task<string> SignalRConnect(
        [Description("Hub path, for example /chat or /hubs/notification")] string hubPath,
        [Description("Optional custom session ID")] string? sessionId = null,
        [Description("Optional JSON object string of request headers")] string? headers = null,
        [Description("Optional SignalR base URL override")] string? signalRBaseUrl = null)
    {
        try
        {
            var parsedHeaders = ParseJsonMap(headers);
            return await SignalRInvocationService.ConnectAsync(
                hubPath,
                signalRBaseUrl ?? options.SignalRBaseUrl,
                parsedHeaders,
                sessionId);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "signalr_subscribe", Title = "SignalR Subscribe")]
    [Description("Registers an event handler on an active SignalR session.")]
    public static Task<string> SignalRSubscribe(
        [Description("Session ID returned by signalr_connect")] string sessionId,
        [Description("Event name emitted by the hub")] string eventName,
        [Description("Number of event arguments expected")] int argCount = 1)
    {
        try
        {
            return Task.FromResult(SignalRInvocationService.SubscribeAsync(sessionId, eventName, argCount));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"ERROR: {ex.Message}");
        }
    }

    [McpServerTool(Name = "signalr_invoke", Title = "SignalR Invoke")]
    [Description("Invokes a hub method on an active SignalR session.")]
    public static async Task<string> SignalRInvoke(
        [Description("Session ID returned by signalr_connect")] string sessionId,
        [Description("Hub method name") ] string methodName,
        [Description("JSON array string for method arguments")] string? argumentsJson = null)
    {
        try
        {
            return await SignalRInvocationService.InvokeAsync(sessionId, methodName, argumentsJson);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "signalr_events", Title = "SignalR Events")]
    [Description("Reads and clears buffered SignalR events for a session.")]
    public static async Task<string> SignalREvents(
        [Description("Session ID returned by signalr_connect")] string sessionId,
        [Description("Optional wait time before draining events")] int durationSeconds = 0)
    {
        try
        {
            return await SignalRInvocationService.DrainEventsAsync(sessionId, durationSeconds);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "signalr_logs", Title = "SignalR Logs")]
    [Description("Returns session connection/action logs.")]
    public static Task<string> SignalRLogs(
        [Description("Session ID returned by signalr_connect")] string sessionId,
        [Description("Maximum number of log entries to return")] int maxEntries = 100)
    {
        try
        {
            return Task.FromResult(SignalRInvocationService.GetLogs(sessionId, maxEntries));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"ERROR: {ex.Message}");
        }
    }

    [McpServerTool(Name = "signalr_disconnect", Title = "SignalR Disconnect")]
    [Description("Stops and disposes an active SignalR session.")]
    public static async Task<string> SignalRDisconnect(
        [Description("Session ID returned by signalr_connect")] string sessionId)
    {
        try
        {
            return await SignalRInvocationService.DisconnectAsync(sessionId);
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
