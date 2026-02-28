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

    public GrpcProxyServiceTests()
    {
        var scannerMock = new Mock<IGrpcServiceScanner>();
        
        var options = new KayaGrpcExplorerOptions
        {
            Middleware = new MiddlewareOptions
            {
                AllowInsecureConnections = true,
                RequestTimeoutSeconds = 30,
                StreamBufferSize = 100
            }
        };

        var sessionManagerMock = new Mock<IStreamingSessionManager>();
        _proxyService = new GrpcProxyService(options, scannerMock.Object, sessionManagerMock.Object);
    }

    [Fact]
    public async Task InvokeMethodAsync_ShouldReturnError_WhenServerUnreachable()
    {
        // Arrange
        var request = new GrpcInvocationRequest
        {
            ServerAddress = "localhost:9999",
            ServiceName = "TestService",
            MethodName = "TestMethod",
            RequestJson = "{}"
        };

        // Act
        var response = await _proxyService.InvokeMethodAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeMethodAsync_ShouldReturnError_WhenInvalidJson()
    {
        // Arrange
        var request = new GrpcInvocationRequest
        {
            ServerAddress = "localhost:5001",
            ServiceName = "TestService",
            MethodName = "TestMethod",
            RequestJson = "invalid json"
        };

        // Act
        var response = await _proxyService.InvokeMethodAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
