using FluentAssertions;
using Kaya.GrpcExplorer.Configuration;
using Kaya.GrpcExplorer.Services;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

/// <summary>
/// Tests for GrpcServiceScanner
/// </summary>
public class GrpcServiceScannerTests
{
    private readonly GrpcServiceScanner _scanner;

    public GrpcServiceScannerTests()
    {
        var options = new KayaGrpcExplorerOptions
        {
            Middleware = new MiddlewareOptions
            {
                AllowInsecureConnections = true,
                RequestTimeoutSeconds = 30
            }
        };

        _scanner = new GrpcServiceScanner(options);
    }

    // -------------------------------------------------------------------------
    // ScanServicesAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ScanServicesAsync_ShouldReturnEmptyList_WhenServerUnreachable()
    {
        var services = await _scanner.ScanServicesAsync("localhost:59999");

        services.Should().NotBeNull();
        services.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanServicesAsync_ShouldCacheResult_OnSecondCall()
    {
        var serverAddress = "localhost:59998";

        var result1 = await _scanner.ScanServicesAsync(serverAddress);
        var result2 = await _scanner.ScanServicesAsync(serverAddress);

        result1.Should().BeSameAs(result2);
    }

    [Fact]
    public async Task ScanServicesAsync_ShouldReturnDifferentObjects_ForDifferentAddresses()
    {
        var result1 = await _scanner.ScanServicesAsync("localhost:59997");
        var result2 = await _scanner.ScanServicesAsync("localhost:59996");

        result1.Should().NotBeSameAs(result2);
    }

    // -------------------------------------------------------------------------
    // ClearCache
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClearCache_ShouldAllowReScan_AfterClearing()
    {
        var serverAddress = "localhost:59995";

        var result1 = await _scanner.ScanServicesAsync(serverAddress);
        _scanner.ClearCache(serverAddress);
        var result2 = await _scanner.ScanServicesAsync(serverAddress);

        // After clearing, a new list object is returned (different reference)
        result1.Should().NotBeSameAs(result2);
    }

    [Fact]
    public void ClearCache_ShouldNotThrow_WhenAddressWasNeverScanned()
    {
        var act = () => _scanner.ClearCache("localhost:59994");

        act.Should().NotThrow();
    }

    [Fact]
    public void ClearCache_ShouldNotThrow_WhenCalledTwiceForSameAddress()
    {
        var act = () =>
        {
            _scanner.ClearCache("localhost:59993");
            _scanner.ClearCache("localhost:59993");
        };

        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // GetCachedDescriptorSet
    // -------------------------------------------------------------------------

    [Fact]
    public void GetCachedDescriptorSet_ShouldReturnNull_WhenNothingIsCached()
    {
        var result = _scanner.GetCachedDescriptorSet("localhost:59992", "SomeService");

        result.Should().BeNull();
    }

    [Fact]
    public void GetCachedDescriptorSet_ShouldReturnNull_ForUnknownService()
    {
        var result = _scanner.GetCachedDescriptorSet("localhost:59991", "NotScannedService");

        result.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // GetCachedMethodDescriptor
    // -------------------------------------------------------------------------

    [Fact]
    public void GetCachedMethodDescriptor_ShouldReturnNull_WhenNothingIsCached()
    {
        var result = _scanner.GetCachedMethodDescriptor("localhost:59990", "SomeService", "SomeMethod");

        result.Should().BeNull();
    }

    [Fact]
    public void GetCachedMethodDescriptor_ShouldReturnNull_ForUnknownCombination()
    {
        var result = _scanner.GetCachedMethodDescriptor("localhost:59989", "Unknown.Service", "NonExistent");

        result.Should().BeNull();
    }
}
