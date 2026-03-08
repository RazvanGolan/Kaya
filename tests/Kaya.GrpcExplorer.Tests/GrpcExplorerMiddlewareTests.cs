using System.Text;
using System.Text.Json;
using FluentAssertions;
using Kaya.GrpcExplorer.Configuration;
using Kaya.GrpcExplorer.Middleware;
using Kaya.GrpcExplorer.Models;
using Kaya.GrpcExplorer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

public class GrpcExplorerMiddlewareTests
{
    private readonly KayaGrpcExplorerOptions _options = new()
    {
        Middleware = new MiddlewareOptions { RoutePrefix = "/grpc-explorer" }
    };

    private readonly Mock<IGrpcUiService> _uiServiceMock = new();
    private readonly Mock<IGrpcServiceScanner> _scannerMock = new();
    private readonly Mock<IGrpcProxyService> _proxyServiceMock = new();
    private readonly Mock<IStreamingSessionManager> _sessionManagerMock = new();

    private DefaultHttpContext BuildContext(string path, string? body = null, string? queryString = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (queryString is not null)
            context.Request.QueryString = new QueryString(queryString);

        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }

        context.Response.Body = new MemoryStream();

        var services = new ServiceCollection();
        services.AddSingleton(_uiServiceMock.Object);
        services.AddSingleton(_scannerMock.Object);
        services.AddSingleton(_proxyServiceMock.Object);
        services.AddSingleton(_sessionManagerMock.Object);
        context.RequestServices = services.BuildServiceProvider();

        return context;
    }

    private GrpcExplorerMiddleware BuildMiddleware(RequestDelegate? next = null)
    {
        next ??= _ => Task.CompletedTask;
        return new GrpcExplorerMiddleware(next, _options);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    // -------------------------------------------------------------------------
    // Pass-through
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldCallNext_WhenPathDoesNotMatchPrefix()
    {
        var nextCalled = false;
        var middleware = BuildMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext("/api/other");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotCallNext_WhenPathMatchesPrefix()
    {
        _uiServiceMock.Setup(u => u.GetUIAsync()).ReturnsAsync("<html/>");
        var nextCalled = false;
        var middleware = BuildMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext("/grpc-explorer");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // UI route
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldServeHtml_ForRootRoute()
    {
        _uiServiceMock.Setup(u => u.GetUIAsync()).ReturnsAsync("<html>explorer</html>");
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer");

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Contain("text/html");
        var body = await ReadResponseBodyAsync(context);
        body.Should().Contain("<html>explorer</html>");
    }

    [Fact]
    public async Task InvokeAsync_ShouldServeHtml_WhenPathHasTrailingSlash()
    {
        _uiServiceMock.Setup(u => u.GetUIAsync()).ReturnsAsync("<html/>");
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/");

        await middleware.InvokeAsync(context);

        _uiServiceMock.Verify(u => u.GetUIAsync(), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldServeHtml_WhenPathIsMixedCase()
    {
        _uiServiceMock.Setup(u => u.GetUIAsync()).ReturnsAsync("<html/>");
        var middleware = BuildMiddleware();
        var context = BuildContext("/GRPC-EXPLORER");

        await middleware.InvokeAsync(context);

        _uiServiceMock.Verify(u => u.GetUIAsync(), Times.Once);
    }

    // -------------------------------------------------------------------------
    // /services route
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldReturnJsonServices_ForServicesRoute()
    {
        var services = new List<GrpcServiceInfo> { new() { ServiceName = "TestService" } };
        _scannerMock.Setup(s => s.ScanServicesAsync(It.IsAny<string>())).ReturnsAsync(services);
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/services", queryString: "?serverAddress=localhost:5001");

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn500_WhenScannerThrows()
    {
        _scannerMock.Setup(s => s.ScanServicesAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("connection refused"));
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/services");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
    }

    // -------------------------------------------------------------------------
    // /invoke route
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldCallProxyService_ForInvokeRoute()
    {
        var invocationResponse = new GrpcInvocationResponse { Success = true, StatusCode = "OK" };
        _proxyServiceMock.Setup(p => p.InvokeMethodAsync(It.IsAny<GrpcInvocationRequest>()))
            .ReturnsAsync(invocationResponse);
        var middleware = BuildMiddleware();
        var body = JsonSerializer.Serialize(new
        {
            serverAddress = "localhost:5001",
            serviceName = "TestService",
            methodName = "TestMethod",
            requestJson = "{}"
        });
        var context = BuildContext("/grpc-explorer/invoke", body);

        await middleware.InvokeAsync(context);

        _proxyServiceMock.Verify(p => p.InvokeMethodAsync(It.IsAny<GrpcInvocationRequest>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn400_WhenInvokeBodyIsNullJson()
    {
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/invoke", "null");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn500_WhenInvokeBodyIsInvalidJson()
    {
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/invoke", "{{not json}}");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn500_WhenProxyThrows()
    {
        _proxyServiceMock.Setup(p => p.InvokeMethodAsync(It.IsAny<GrpcInvocationRequest>()))
            .ThrowsAsync(new Exception("proxy error"));
        var middleware = BuildMiddleware();
        var body = JsonSerializer.Serialize(new
        {
            serverAddress = "localhost:5001",
            serviceName = "S",
            methodName = "M"
        });
        var context = BuildContext("/grpc-explorer/invoke", body);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
    }

    // -------------------------------------------------------------------------
    // /stream/start route
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldReturnSessionId_ForStreamStartRoute()
    {
        _proxyServiceMock.Setup(p => p.StartStreamAsync(It.IsAny<StreamStartRequest>()))
            .ReturnsAsync("session-abc");
        var middleware = BuildMiddleware();
        var body = JsonSerializer.Serialize(new
        {
            serverAddress = "localhost:5001",
            serviceName = "S",
            methodName = "M"
        });
        var context = BuildContext("/grpc-explorer/stream/start", body);

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Contain("application/json");
        var responseBody = await ReadResponseBodyAsync(context);
        responseBody.Should().Contain("session-abc");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn400_WhenStreamStartBodyIsNull()
    {
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/stream/start", "null");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn500_WhenStreamStartThrows()
    {
        _proxyServiceMock.Setup(p => p.StartStreamAsync(It.IsAny<StreamStartRequest>()))
            .ThrowsAsync(new InvalidOperationException("method not found"));
        var middleware = BuildMiddleware();
        var body = JsonSerializer.Serialize(new { serverAddress = "localhost:5001", serviceName = "S", methodName = "M" });
        var context = BuildContext("/grpc-explorer/stream/start", body);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
    }

    // -------------------------------------------------------------------------
    // /stream/send route
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldCallSendMessage_ForStreamSendRoute()
    {
        _proxyServiceMock.Setup(p => p.SendMessageAsync(It.IsAny<StreamSendRequest>()))
            .Returns(Task.CompletedTask);
        var middleware = BuildMiddleware();
        var body = JsonSerializer.Serialize(new { sessionId = "sess1", messageJson = "{}" });
        var context = BuildContext("/grpc-explorer/stream/send", body);

        await middleware.InvokeAsync(context);

        _proxyServiceMock.Verify(p => p.SendMessageAsync(It.IsAny<StreamSendRequest>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn400_WhenStreamSendBodyIsNull()
    {
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/stream/send", "null");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    // -------------------------------------------------------------------------
    // /stream/end route
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldCallEndStream_ForStreamEndRoute()
    {
        _proxyServiceMock.Setup(p => p.EndStreamAsync(It.IsAny<StreamEndRequest>()))
            .Returns(Task.CompletedTask);
        var middleware = BuildMiddleware();
        var body = JsonSerializer.Serialize(new { sessionId = "sess1" });
        var context = BuildContext("/grpc-explorer/stream/end", body);

        await middleware.InvokeAsync(context);

        _proxyServiceMock.Verify(p => p.EndStreamAsync(It.IsAny<StreamEndRequest>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn400_WhenStreamEndBodyIsNull()
    {
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/stream/end", "null");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    // -------------------------------------------------------------------------
    // /stream/events/{sessionId} route
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldReturn404_WhenSessionNotFoundForEvents()
    {
        _sessionManagerMock.Setup(m => m.Get(It.IsAny<string>())).Returns((StreamingSession?)null);
        var middleware = BuildMiddleware();
        var context = BuildContext("/grpc-explorer/stream/events/missing-session");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(404);
    }

    // -------------------------------------------------------------------------
    // Custom route prefix
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ShouldUseCustomRoutePrefix()
    {
        var customOptions = new KayaGrpcExplorerOptions
        {
            Middleware = new MiddlewareOptions { RoutePrefix = "/my-grpc" }
        };
        _uiServiceMock.Setup(u => u.GetUIAsync()).ReturnsAsync("<html/>");
        var middleware = new GrpcExplorerMiddleware(_ => Task.CompletedTask, customOptions);
        var context = BuildContext("/my-grpc");

        await middleware.InvokeAsync(context);

        _uiServiceMock.Verify(u => u.GetUIAsync(), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotMatch_WhenPrefixDoesNotAlignWithCustomPrefix()
    {
        var customOptions = new KayaGrpcExplorerOptions
        {
            Middleware = new MiddlewareOptions { RoutePrefix = "/my-grpc" }
        };
        var nextCalled = false;
        var middleware = new GrpcExplorerMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, customOptions);
        var context = BuildContext("/grpc-explorer");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
