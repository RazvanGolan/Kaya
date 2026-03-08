using FluentAssertions;
using Kaya.GrpcExplorer.Helpers;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

public class CompiledMessageTypeCacheTests
{
    public CompiledMessageTypeCacheTests()
    {
        // Start each test with a clean state
        CompiledMessageTypeCache.ClearCache();
    }

    [Fact]
    public void FindGeneratedType_ShouldReturnNull_ForUnknownTypeName()
    {
        var result = CompiledMessageTypeCache.FindGeneratedType("TypeThatDefinitelyDoesNotExist_XYZ_12345");

        result.Should().BeNull();
    }

    [Fact]
    public void FindGeneratedType_ShouldReturnSameResult_OnRepeatedCalls()
    {
        // First call scans assemblies; second call uses the cache
        var result1 = CompiledMessageTypeCache.FindGeneratedType("AlsoNonExistent_ABCDE_99999");
        var result2 = CompiledMessageTypeCache.FindGeneratedType("AlsoNonExistent_ABCDE_99999");

        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    [Fact]
    public void FindGeneratedType_ShouldReturnNull_ForEmptyString()
    {
        var result = CompiledMessageTypeCache.FindGeneratedType(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void ClearCache_ShouldNotThrow_WhenCalledRepeatedly()
    {
        var act = () =>
        {
            CompiledMessageTypeCache.ClearCache();
            CompiledMessageTypeCache.ClearCache();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ClearCache_ShouldAllowReScanOnNextFind()
    {
        // Warm up the cache
        CompiledMessageTypeCache.FindGeneratedType("WarmUp_Type_XYZ");

        // Clear and call again — should not throw and should still return null
        CompiledMessageTypeCache.ClearCache();

        var result = CompiledMessageTypeCache.FindGeneratedType("WarmUp_Type_XYZ");
        result.Should().BeNull();
    }

    [Fact]
    public void GetParser_ShouldReturnNull_ForNonProtobufType()
    {
        var result = CompiledMessageTypeCache.GetParser(typeof(string));

        result.Should().BeNull();
    }

    [Fact]
    public void GetParser_ShouldReturnNull_ForTypeWithNoParserProperty()
    {
        var result = CompiledMessageTypeCache.GetParser(typeof(object));

        result.Should().BeNull();
    }
}
