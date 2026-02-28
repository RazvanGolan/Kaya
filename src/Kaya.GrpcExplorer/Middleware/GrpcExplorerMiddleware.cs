using System.Text;
using System.Text.Json;
using Kaya.GrpcExplorer.Configuration;
using Kaya.GrpcExplorer.Models;
using Kaya.GrpcExplorer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.GrpcExplorer.Middleware;

/// <summary>
/// Middleware for serving the gRPC Explorer UI and handling API requests
/// </summary>
public class GrpcExplorerMiddleware(RequestDelegate next, KayaGrpcExplorerOptions options)
{
    private readonly string _routePrefix = options.Middleware.RoutePrefix.TrimEnd('/');

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Check if request is for gRPC Explorer
        if (path.StartsWith(_routePrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (path == $"{_routePrefix.ToLower()}" || path == $"{_routePrefix.ToLower()}/")
            {
                // Serve UI
                await ServeUIAsync(context);
                return;
            }

            if (path == $"{_routePrefix.ToLower()}/services")
            {
                // Get services from a server
                await GetServicesAsync(context);
                return;
            }

            if (path == $"{_routePrefix.ToLower()}/invoke")
            {
                // Invoke a method
                await InvokeMethodAsync(context);
                return;
            }

            if (path == $"{_routePrefix.ToLower()}/stream/start")
            {
                await StreamStartAsync(context);
                return;
            }

            if (path == $"{_routePrefix.ToLower()}/stream/send")
            {
                await StreamSendAsync(context);
                return;
            }

            if (path == $"{_routePrefix.ToLower()}/stream/end")
            {
                await StreamEndAsync(context);
                return;
            }

            // SSE: /grpc-explorer/stream/events/{sessionId}
            var streamEventsPrefix = $"{_routePrefix.ToLower()}/stream/events/";
            if (path.StartsWith(streamEventsPrefix))
            {
                var sessionId = path[streamEventsPrefix.Length..];
                await StreamEventsAsync(context, sessionId);
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// Serves the gRPC Explorer UI
    /// </summary>
    private static async Task ServeUIAsync(HttpContext context)
    {
        var uiService = context.RequestServices.GetRequiredService<IGrpcUiService>();
        var html = await uiService.GetUIAsync();

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
    }

    /// <summary>
    /// Gets services from a gRPC server
    /// </summary>
    private async Task GetServicesAsync(HttpContext context)
    {
        try
        {
            var serverAddress = context.Request.Query["serverAddress"].ToString();

            var scanner = context.RequestServices.GetRequiredService<IGrpcServiceScanner>();
            var services = await scanner.ScanServicesAsync(serverAddress);

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(services);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Invokes a gRPC method
    /// </summary>
    private static async Task InvokeMethodAsync(HttpContext context)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<GrpcInvocationRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request is null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid request" });
                return;
            }

            var proxyService = context.RequestServices.GetRequiredService<IGrpcProxyService>();
            var response = await proxyService.InvokeMethodAsync(request);

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(response);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Starts an interactive streaming session
    /// </summary>
    private static async Task StreamStartAsync(HttpContext context)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<StreamStartRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request is null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid request" });
                return;
            }

            var proxyService = context.RequestServices.GetRequiredService<IGrpcProxyService>();
            var sessionId = await proxyService.StartStreamAsync(request);

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { sessionId });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Sends one message into an active streaming session
    /// </summary>
    private static async Task StreamSendAsync(HttpContext context)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<StreamSendRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request is null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid request" });
                return;
            }

            var proxyService = context.RequestServices.GetRequiredService<IGrpcProxyService>();
            await proxyService.SendMessageAsync(request);

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { ok = true });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Completes the client-side request stream of an active session
    /// </summary>
    private static async Task StreamEndAsync(HttpContext context)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<StreamEndRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request is null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid request" });
                return;
            }

            var proxyService = context.RequestServices.GetRequiredService<IGrpcProxyService>();
            await proxyService.EndStreamAsync(request);

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { ok = true });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    /// <summary>
    /// SSE endpoint: streams gRPC responses to the browser in real time
    /// </summary>
    private static async Task StreamEventsAsync(HttpContext context, string sessionId)
    {
        var sessionManager = context.RequestServices.GetRequiredService<IStreamingSessionManager>();
        var session = sessionManager.Get(sessionId);

        if (session is null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = $"Session '{sessionId}' not found." });
            return;
        }

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        context.Response.Headers.Connection = "keep-alive";

        var ct = context.RequestAborted;

        try
        {
            await foreach (var evt in session.Events.ReadAllAsync(ct))
            {
                var eventName = evt.Type switch
                {
                    SseEventType.Message => "message",
                    SseEventType.Complete => "complete",
                    SseEventType.Error => "error",
                    _ => "message"
                };

                var sseData = evt.Payload.Replace("\n", "\ndata: ");
                var line = $"event: {eventName}\ndata: {sseData}\n\n";
                await context.Response.WriteAsync(line, Encoding.UTF8, ct);
                await context.Response.Body.FlushAsync(ct);
            }
        }
        finally
        {
            await sessionManager.RemoveAsync(sessionId);
        }
    }
}
