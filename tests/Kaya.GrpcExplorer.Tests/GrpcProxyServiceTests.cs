using FluentAssertions;
using Kaya.GrpcExplorer.Configuration;
using Kaya.GrpcExplorer.Models;
using Kaya.GrpcExplorer.Services;
using Moq;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

/// <summary>
/// Tests for GrpcProxyService
/// </summary>
public class GrpcProxyServiceTests
{
    private readonly GrpcProxyService _proxyService;
    private readonly Mock<IGrpcServiceScanner> _scannerMock = new();
    private readonly Mock<IStreamingSessionManager> _sessionManagerMock = new();

    public GrpcProxyServiceTests()
    {
        var options = new KayaGrpcExplorerOptions
        {
            Middleware = new MiddlewareOptions
            {
                AllowInsecureConnections = true,
                RequestTimeoutSeconds = 30,
                StreamBufferSize = 100
            }
        };

        _proxyService = new GrpcProxyService(options, _scannerMock.Object, _sessionManagerMock.Object);
    }

    // -------------------------------------------------------------------------
    // InvokeMethodAsync — error-path scenarios (no live server needed)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeMethodAsync_ShouldReturnError_WhenServiceNotFound()
    {
        _scannerMock.Setup(s => s.ScanServicesAsync(It.IsAny<string>())).ReturnsAsync([]);

        var response = await _proxyService.InvokeMethodAsync(new GrpcInvocationRequest
        {
            ServerAddress = "localhost:59999",
            ServiceName = "NoSuchService",
            MethodName = "TestMethod"
        });

        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Contain("NoSuchService");
        response.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task InvokeMethodAsync_ShouldReturnError_WhenMethodNotFound()
    {
        _scannerMock.Setup(s => s.ScanServicesAsync(It.IsAny<string>())).ReturnsAsync(
        [
            new GrpcServiceInfo { ServiceName = "orders.OrderService", Methods = [] }
        ]);

        var response = await _proxyService.InvokeMethodAsync(new GrpcInvocationRequest
        {
            ServerAddress = "localhost:59999",
            ServiceName = "orders.OrderService",
            MethodName = "NonExistentMethod"
        });

        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Contain("NonExistentMethod");
        response.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task InvokeMethodAsync_ShouldSetDurationMs_OnError()
    {
        _scannerMock.Setup(s => s.ScanServicesAsync(It.IsAny<string>())).ReturnsAsync([]);

        var response = await _proxyService.InvokeMethodAsync(new GrpcInvocationRequest
        {
            ServerAddress = "localhost:59999",
            ServiceName = "SomeService",
            MethodName = "SomeMethod"
        });

        response.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task InvokeMethodAsync_ShouldReturnError_WhenScannerThrowsGenericException()
    {
        _scannerMock.Setup(s => s.ScanServicesAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("connection refused"));

        var response = await _proxyService.InvokeMethodAsync(new GrpcInvocationRequest
        {
            ServerAddress = "localhost:59999",
            ServiceName = "SomeService",
            MethodName = "SomeMethod"
        });

        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Contain("connection refused");
    }

    [Fact]
    public async Task InvokeMethodAsync_ShouldReturnFalseSuccess_WhenScannerReturnsNullList()
    {
        // Default Moq returns null for Task<List<T>>; the method catches the NullReferenceException
        _scannerMock.Setup(s => s.ScanServicesAsync(It.IsAny<string>()))
            .ReturnsAsync((List<GrpcServiceInfo>?)null!);

        var response = await _proxyService.InvokeMethodAsync(new GrpcInvocationRequest
        {
            ServerAddress = "localhost:59999",
            ServiceName = "SomeService",
            MethodName = "SomeMethod"
        });

        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // -------------------------------------------------------------------------
    // SendMessageAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendMessageAsync_ShouldThrowInvalidOperationException_WhenSessionNotFound()
    {
        _sessionManagerMock.Setup(m => m.Get("nonexistent")).Returns((StreamingSession?)null);

        var act = async () => await _proxyService.SendMessageAsync(
            new StreamSendRequest { SessionId = "nonexistent", MessageJson = "{}" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public async Task SendMessageAsync_ShouldThrowInvalidOperationException_WhenSessionHasNoRequestWriter()
    {
        var session = new StreamingSession { MethodDescriptor = null! }; // RequestWriter is null
        _sessionManagerMock.Setup(m => m.Get("sess1")).Returns(session);

        var act = async () => await _proxyService.SendMessageAsync(
            new StreamSendRequest { SessionId = "sess1", MessageJson = "{}" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not accept client messages*");

        // cleanup
        await session.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // EndStreamAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EndStreamAsync_ShouldThrowInvalidOperationException_WhenSessionNotFound()
    {
        _sessionManagerMock.Setup(m => m.Get("nonexistent")).Returns((StreamingSession?)null);

        var act = async () => await _proxyService.EndStreamAsync(
            new StreamEndRequest { SessionId = "nonexistent" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public async Task EndStreamAsync_ShouldThrowInvalidOperationException_WhenSessionHasNoRequestWriter()
    {
        var session = new StreamingSession { MethodDescriptor = null! }; // RequestWriter is null
        _sessionManagerMock.Setup(m => m.Get("sess2")).Returns(session);

        var act = async () => await _proxyService.EndStreamAsync(
            new StreamEndRequest { SessionId = "sess2" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not accept client messages*");

        // cleanup
        await session.DisposeAsync();
    }
}
