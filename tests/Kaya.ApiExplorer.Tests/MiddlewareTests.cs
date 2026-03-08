using System.Text;
using System.Text.Json;
using Kaya.ApiExplorer.Middleware;
using Kaya.ApiExplorer.Models;
using Kaya.ApiExplorer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.ApiExplorer.Tests;

// ─── Shared helpers ───────────────────────────────────────────────────────────

file static class MiddlewareTestHelpers
{
    public static (DefaultHttpContext ctx, MemoryStream body) CreateContext(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        var body = new MemoryStream();
        ctx.Response.Body = body;
        return (ctx, body);
    }

    public static string ReadBody(MemoryStream stream)
    {
        stream.Position = 0;
        return new StreamReader(stream, Encoding.UTF8).ReadToEnd();
    }
}

// ─── Fakes for ApiExplorerMiddleware ─────────────────────────────────────────

file sealed class FakeUIService : IUIService
{
    public Task<string> GetUIAsync() => Task.FromResult("<html><body>kaya explorer</body></html>");
}

file sealed class ThrowingUIService : IUIService
{
    public Task<string> GetUIAsync() => throw new InvalidOperationException("UI crashed");
}

file sealed class FakeEndpointScanner : IEndpointScanner
{
    public ApiDocumentation ScanEndpoints(IServiceProvider sp) =>
        new() { Title = "Test API", Version = "1.0.0", Controllers = [] };
}

file sealed class FakeOpenApiExportService : IOpenApiExportService
{
    public object GenerateOpenApiSpec(ApiDocumentation doc) =>
        new Dictionary<string, object> { ["openapi"] = "3.0.3", ["info"] = doc.Title };
}

// ─── Fakes for SignalRDebugMiddleware ─────────────────────────────────────────

file sealed class FakeSignalRUIService : ISignalRUIService
{
    public Task<string> GetUIAsync() => Task.FromResult("<html><body>signalr debug</body></html>");
}

file sealed class ThrowingSignalRUIService : ISignalRUIService
{
    public Task<string> GetUIAsync() => throw new InvalidOperationException("UI crashed");
}

file sealed class FakeSignalRHubScanner : ISignalRHubScanner
{
    public SignalRDocumentation ScanHubs(IServiceProvider sp) =>
        new() { Title = "SignalR Hubs", Version = "1.0.0", Hubs = [] };
}

file sealed class ThrowingSignalRHubScanner : ISignalRHubScanner
{
    public SignalRDocumentation ScanHubs(IServiceProvider sp) =>
        throw new InvalidOperationException("Scanner crashed");
}

// ─── ApiExplorerMiddleware tests ──────────────────────────────────────────────

public class ApiExplorerMiddlewareTests
{
    private static IServiceProvider BuildServices(
        IUIService? ui = null,
        IEndpointScanner? scanner = null,
        IOpenApiExportService? export = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ui ?? (IUIService)new FakeUIService());
        services.AddSingleton(scanner ?? (IEndpointScanner)new FakeEndpointScanner());
        services.AddSingleton(export ?? (IOpenApiExportService)new FakeOpenApiExportService());
        return services.BuildServiceProvider();
    }

    // ── Root path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_RootPath_ServesHtml()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya");
        ctx.RequestServices = BuildServices();
        await new ApiExplorerMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("text/html", ctx.Response.ContentType);
        Assert.Contains("kaya explorer", MiddlewareTestHelpers.ReadBody(body));
    }

    [Fact]
    public async Task InvokeAsync_RootPathWithTrailingSlash_ServesHtml()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya/");
        ctx.RequestServices = BuildServices();
        await new ApiExplorerMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("text/html", ctx.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_RootPathUpperCase_ServesHtml()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/KAYA");
        ctx.RequestServices = BuildServices();
        await new ApiExplorerMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("text/html", ctx.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_RootPath_DoesNotCallNext()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/kaya");
        ctx.RequestServices = BuildServices();
        var nextCalled = false;
        await new ApiExplorerMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.False(nextCalled);
    }

    // ── /api-docs ────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ApiDocsPath_ServesJsonDocumentation()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya/api-docs");
        ctx.RequestServices = BuildServices();
        await new ApiExplorerMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("application/json", ctx.Response.ContentType);
        Assert.Contains("Test API", MiddlewareTestHelpers.ReadBody(body));
    }

    [Fact]
    public async Task InvokeAsync_ApiDocsPathUpperCase_ServesJsonDocumentation()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/KAYA/API-DOCS");
        ctx.RequestServices = BuildServices();
        await new ApiExplorerMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("application/json", ctx.Response.ContentType);
    }

    // ── /openapi.json ─────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_OpenApiJsonPath_ServesOpenApiSpec()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya/openapi.json");
        ctx.RequestServices = BuildServices();
        await new ApiExplorerMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("application/json", ctx.Response.ContentType);
        Assert.Contains("3.0.3", MiddlewareTestHelpers.ReadBody(body));
    }

    // ── Unmatched paths fall through ─────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UnknownPathUnderPrefix_CallsNext()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/kaya/unknown");
        ctx.RequestServices = BuildServices();
        var nextCalled = false;
        await new ApiExplorerMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_UnrelatedPath_CallsNext()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/other/path");
        ctx.RequestServices = BuildServices();
        var nextCalled = false;
        await new ApiExplorerMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(nextCalled);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UIServiceThrows_Returns500WithMessage()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya");
        ctx.RequestServices = BuildServices(ui: new ThrowingUIService());
        await new ApiExplorerMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(500, ctx.Response.StatusCode);
        Assert.Contains("Failed to load", MiddlewareTestHelpers.ReadBody(body));
    }

    // ── Custom route prefix ───────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_CustomRoutePrefix_ServesHtmlAtCustomPath()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/explorer");
        ctx.RequestServices = BuildServices();
        await new ApiExplorerMiddleware(_ => Task.CompletedTask, "/explorer").InvokeAsync(ctx);

        Assert.Equal("text/html", ctx.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_CustomRoutePrefix_DefaultPathNoLongerMatches()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/kaya");
        ctx.RequestServices = BuildServices();
        var nextCalled = false;
        await new ApiExplorerMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, "/explorer").InvokeAsync(ctx);

        Assert.True(nextCalled);
    }
}

// ─── SignalRDebugMiddleware tests ─────────────────────────────────────────────

public class SignalRDebugMiddlewareTests
{
    private static IServiceProvider BuildServices(
        ISignalRUIService? ui = null,
        ISignalRHubScanner? scanner = null)
    {
        var services = new ServiceCollection();
        if (ui is not null)      services.AddSingleton(ui);
        if (scanner is not null) services.AddSingleton(scanner);
        return services.BuildServiceProvider();
    }

    // ── Root path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_RootPath_ServesHtml()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya-signalr");
        ctx.RequestServices = BuildServices(ui: new FakeSignalRUIService());
        await new SignalRDebugMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("text/html", ctx.Response.ContentType);
        Assert.Contains("signalr debug", MiddlewareTestHelpers.ReadBody(body));
    }

    [Fact]
    public async Task InvokeAsync_RootPathWithTrailingSlash_ServesHtml()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/kaya-signalr/");
        ctx.RequestServices = BuildServices(ui: new FakeSignalRUIService());
        await new SignalRDebugMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("text/html", ctx.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_RootPathUpperCase_ServesHtml()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/KAYA-SIGNALR");
        ctx.RequestServices = BuildServices(ui: new FakeSignalRUIService());
        await new SignalRDebugMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("text/html", ctx.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_RootPath_DoesNotCallNext()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/kaya-signalr");
        ctx.RequestServices = BuildServices(ui: new FakeSignalRUIService());
        var nextCalled = false;
        await new SignalRDebugMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.False(nextCalled);
    }

    // ── /hubs ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_HubsPath_ServesHubsJson()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya-signalr/hubs");
        ctx.RequestServices = BuildServices(scanner: new FakeSignalRHubScanner());
        await new SignalRDebugMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal("application/json", ctx.Response.ContentType);
        Assert.Contains("SignalR Hubs", MiddlewareTestHelpers.ReadBody(body));
    }

    [Fact]
    public async Task InvokeAsync_HubsPath_DoesNotCallNext()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/kaya-signalr/hubs");
        ctx.RequestServices = BuildServices(scanner: new FakeSignalRHubScanner());
        var nextCalled = false;
        await new SignalRDebugMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.False(nextCalled);
    }

    // ── Unmatched paths fall through ─────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UnknownPathUnderPrefix_CallsNext()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/kaya-signalr/unknown");
        ctx.RequestServices = BuildServices();
        var nextCalled = false;
        await new SignalRDebugMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_UnrelatedPath_CallsNext()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/other");
        ctx.RequestServices = BuildServices();
        var nextCalled = false;
        await new SignalRDebugMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(nextCalled);
    }

    // ── Missing services → 503 ────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UIServiceNotRegistered_Returns503()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya-signalr");
        ctx.RequestServices = BuildServices(); // nothing registered
        await new SignalRDebugMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(503, ctx.Response.StatusCode);
        Assert.Contains("not available", MiddlewareTestHelpers.ReadBody(body));
    }

    [Fact]
    public async Task InvokeAsync_HubScannerNotRegistered_Returns503()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya-signalr/hubs");
        ctx.RequestServices = BuildServices(); // nothing registered
        await new SignalRDebugMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(503, ctx.Response.StatusCode);
        Assert.Contains("not available", MiddlewareTestHelpers.ReadBody(body));
    }

    // ── Service exceptions → 500 ──────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UIServiceThrows_Returns500WithMessage()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya-signalr");
        ctx.RequestServices = BuildServices(ui: new ThrowingSignalRUIService());
        await new SignalRDebugMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(500, ctx.Response.StatusCode);
        Assert.Contains("Failed to load", MiddlewareTestHelpers.ReadBody(body));
    }

    [Fact]
    public async Task InvokeAsync_HubScannerThrows_Returns500WithMessage()
    {
        var (ctx, body) = MiddlewareTestHelpers.CreateContext("/kaya-signalr/hubs");
        ctx.RequestServices = BuildServices(scanner: new ThrowingSignalRHubScanner());
        await new SignalRDebugMiddleware(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(500, ctx.Response.StatusCode);
        Assert.Contains("Failed to scan", MiddlewareTestHelpers.ReadBody(body));
    }

    // ── Custom route prefix ───────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_CustomRoutePrefix_ServesHtmlAtCustomPath()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/my-signalr");
        ctx.RequestServices = BuildServices(ui: new FakeSignalRUIService());
        await new SignalRDebugMiddleware(_ => Task.CompletedTask, "/my-signalr").InvokeAsync(ctx);

        Assert.Equal("text/html", ctx.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_CustomRoutePrefix_DefaultPathNoLongerMatches()
    {
        var (ctx, _) = MiddlewareTestHelpers.CreateContext("/kaya-signalr");
        ctx.RequestServices = BuildServices(ui: new FakeSignalRUIService());
        var nextCalled = false;
        await new SignalRDebugMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, "/my-signalr").InvokeAsync(ctx);

        Assert.True(nextCalled);
    }
}
