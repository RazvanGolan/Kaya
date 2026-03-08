using System.Text.Json;
using Kaya.ApiExplorer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.ApiExplorer.Middleware;

public class ApiExplorerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _routePrefix;

    public ApiExplorerMiddleware(RequestDelegate next, string routePrefix = "/kaya")
    {
        _next = next;
        _routePrefix = routePrefix.TrimEnd('/');
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        if (path.StartsWith(_routePrefix.ToLower()))
        {
            if (path == $"{_routePrefix.ToLower()}" || path == $"{_routePrefix.ToLower()}/")
            {
                await ServeUI(context);
                return;
            }

            if (path == $"{_routePrefix.ToLower()}/api-docs")
            {
                await ServeApiDocs(context);
                return;
            }

            if (path == $"{_routePrefix.ToLower()}/openapi.json")
            {
                await ServeOpenApiSpec(context);
                return;
            }
        }

        await _next(context);
    }

    private static async Task ServeUI(HttpContext context)
    {
        try
        {
            var uiService = context.RequestServices.GetRequiredService<IUIService>();
            var html = await uiService.GetUIAsync();
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);
        }
        catch (Exception)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Failed to load Kaya API Explorer UI");
        }
    }

    private static async Task ServeApiDocs(HttpContext context)
    {
        var scanner = context.RequestServices.GetRequiredService<IEndpointScanner>();
        var documentation = scanner.ScanEndpoints(context.RequestServices);
        
        var json = JsonSerializer.Serialize(documentation, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(json);
    }

    private static async Task ServeOpenApiSpec(HttpContext context)
    {
        var scanner = context.RequestServices.GetRequiredService<IEndpointScanner>();
        var exportService = context.RequestServices.GetRequiredService<IOpenApiExportService>();

        var documentation = scanner.ScanEndpoints(context.RequestServices);
        var spec = exportService.GenerateOpenApiSpec(documentation);

        var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(json);
    }
}
