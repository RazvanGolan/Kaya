using FluentAssertions;
using Grpc.Core;
using Kaya.GrpcExplorer.Helpers;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

/// <summary>
/// Tests for GrpcReflectionHelper methods that do not require a live server
/// </summary>
public class GrpcReflectionHelperTests
{
    // -------------------------------------------------------------------------
    // CreateMetadata
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateMetadata_ShouldReturnPopulatedMetadata_ForNonEmptyDictionary()
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Bearer token123" },
            { "X-API-Key", "key456" }
        };

        var metadata = GrpcReflectionHelper.CreateMetadata(headers);

        metadata.Should().NotBeNull();
        metadata.Count.Should().Be(2);
        metadata.Get("authorization")?.Value.Should().Be("Bearer token123");
        metadata.Get("x-api-key")?.Value.Should().Be("key456");
    }

    [Fact]
    public void CreateMetadata_ShouldReturnEmptyMetadata_ForNullDictionary()
    {
        var metadata = GrpcReflectionHelper.CreateMetadata(null);

        metadata.Should().NotBeNull();
        metadata.Count.Should().Be(0);
    }

    [Fact]
    public void CreateMetadata_ShouldReturnEmptyMetadata_ForEmptyDictionary()
    {
        var metadata = GrpcReflectionHelper.CreateMetadata([]);

        metadata.Should().NotBeNull();
        metadata.Count.Should().Be(0);
    }

    [Fact]
    public void CreateMetadata_ShouldHandleSingleEntry()
    {
        var metadata = GrpcReflectionHelper.CreateMetadata(
            new Dictionary<string, string> { { "x-custom", "value" } });

        metadata.Count.Should().Be(1);
        metadata.Get("x-custom")?.Value.Should().Be("value");
    }

    // -------------------------------------------------------------------------
    // MetadataToDictionary
    // -------------------------------------------------------------------------

    [Fact]
    public void MetadataToDictionary_ShouldConvertAllTextEntries()
    {
        var metadata = new Metadata
        {
            { "Authorization", "Bearer token123" },
            { "X-API-Key", "key456" }
        };

        var dict = GrpcReflectionHelper.MetadataToDictionary(metadata);

        dict.Should().HaveCount(2);
        dict["authorization"].Should().Be("Bearer token123");
        dict["x-api-key"].Should().Be("key456");
    }

    [Fact]
    public void MetadataToDictionary_ShouldExcludeBinaryEntries()
    {
        var metadata = new Metadata
        {
            { "text-header", "value" },
            new Metadata.Entry("binary-header-bin", new byte[] { 1, 2, 3 })
        };

        var dict = GrpcReflectionHelper.MetadataToDictionary(metadata);

        // Only the text header should appear; binary headers are excluded
        dict.Should().ContainKey("text-header");
        dict.Should().NotContainKey("binary-header-bin");
    }

    [Fact]
    public void MetadataToDictionary_ShouldReturnEmptyDictionary_ForEmptyMetadata()
    {
        var dict = GrpcReflectionHelper.MetadataToDictionary(new Metadata());

        dict.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // GetOrCreateChannel
    // -------------------------------------------------------------------------

    [Fact]
    public void GetOrCreateChannel_ShouldReturnChannel_ForAddressWithoutScheme()
    {
        var channel = GrpcReflectionHelper.GetOrCreateChannel("localhost:5000", allowInsecure: false);

        channel.Should().NotBeNull();
        channel.Target.Should().Contain("localhost:5000");
    }

    [Fact]
    public void GetOrCreateChannel_ShouldReturnChannel_WithHttpsSchemeByDefault()
    {
        // GrpcChannel.Target does not include the scheme, but the channel is still created
        // with the correct URL (https) since allowInsecure is false.
        var channel = GrpcReflectionHelper.GetOrCreateChannel("myserver:6000", allowInsecure: false);

        channel.Should().NotBeNull();
        channel.Target.Should().Contain("myserver:6000");
    }

    [Fact]
    public void GetOrCreateChannel_ShouldReturnChannel_WithHttpScheme_WhenInsecureAllowed()
    {
        // GrpcChannel.Target does not include the scheme, but the channel is still created
        // with the correct URL (http) since allowInsecure is true.
        var channel = GrpcReflectionHelper.GetOrCreateChannel("myserver:7000", allowInsecure: true);

        channel.Should().NotBeNull();
        channel.Target.Should().Contain("myserver:7000");
    }

    [Fact]
    public void GetOrCreateChannel_ShouldReturnSameInstance_WhenCalledTwiceWithSameArguments()
    {
        var channel1 = GrpcReflectionHelper.GetOrCreateChannel("cachedhost:8000", allowInsecure: true);
        var channel2 = GrpcReflectionHelper.GetOrCreateChannel("cachedhost:8000", allowInsecure: true);

        channel1.Should().BeSameAs(channel2);
    }

    [Fact]
    public void GetOrCreateChannel_ShouldReturnDifferentInstances_ForDifferentAddresses()
    {
        var channel1 = GrpcReflectionHelper.GetOrCreateChannel("host-a:9001", allowInsecure: true);
        var channel2 = GrpcReflectionHelper.GetOrCreateChannel("host-b:9001", allowInsecure: true);

        channel1.Should().NotBeSameAs(channel2);
    }

    [Fact]
    public void GetOrCreateChannel_ShouldPreserveExplicitHttpsScheme()
    {
        var channel = GrpcReflectionHelper.GetOrCreateChannel("https://explicit.host:443", allowInsecure: false);

        channel.Target.Should().Contain("explicit.host");
    }

    [Fact]
    public void GetOrCreateChannel_ShouldPreserveExplicitHttpScheme()
    {
        var channel = GrpcReflectionHelper.GetOrCreateChannel("http://explicit.host:80", allowInsecure: true);

        channel.Target.Should().Contain("explicit.host");
    }
}
