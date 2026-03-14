using System.Reflection;
using Kaya.ApiExplorer.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Kaya.ApiExplorer.Tests;

/// <summary>
/// Test class for XML documentation helper
/// </summary>
public class XmlDocumentationHelperTests
{
    /// <summary>
    /// Test method for getting type summary
    /// </summary>
    [Fact]
    public void GetTypeSummary_ReturnsDocumentation_WhenXmlExists()
    {
        // Arrange
        var type = typeof(XmlDocumentationHelperTests);

        // Act
        var summary = XmlDocumentationHelper.GetTypeSummary(type);

        // Assert
        Assert.NotNull(summary);
        Assert.Contains("Test class for XML documentation helper", summary);
    }

    /// <summary>
    /// Sample method for testing method documentation
    /// </summary>
    /// <param name="value">A test parameter</param>
    /// <returns>Returns the value multiplied by 2</returns>
    public int SampleMethod(int value)
    {
        return value * 2;
    }

    [Fact]
    public void GetMethodSummary_ReturnsDocumentation_WhenXmlExists()
    {
        // Arrange
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(SampleMethod));

        // Act
        var summary = XmlDocumentationHelper.GetMethodSummary(method!);

        // Assert
        Assert.NotNull(summary);
        Assert.Contains("Sample method for testing method documentation", summary);
    }

    [Fact]
    public void GetParameterDescription_ReturnsDocumentation_WhenXmlExists()
    {
        // Arrange
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(SampleMethod));

        // Act
        var description = XmlDocumentationHelper.GetParameterDescription(method!, "value");

        // Assert
        Assert.NotNull(description);
        Assert.Contains("test parameter", description);
    }

    [Fact]
    public void GetReturnsDescription_ReturnsDocumentation_WhenXmlExists()
    {
        // Arrange
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(SampleMethod));

        // Act
        var returnsDesc = XmlDocumentationHelper.GetReturnsDescription(method!);

        // Assert
        Assert.NotNull(returnsDesc);
        Assert.Contains("multiplied by 2", returnsDesc);
    }

    [Fact]
    public void GetTypeSummary_ReturnsNull_WhenNoDocumentation()
    {
        // Arrange
        var type = typeof(object); // System types won't have our XML docs

        // Act
        var summary = XmlDocumentationHelper.GetTypeSummary(type);

        // Assert
        Assert.Null(summary);
    }

    // -------------------------------------------------------------------------
    // GetResponseDescription tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sample method with per-status-code response docs
    /// </summary>
    /// <response code="200">Request succeeded</response>
    /// <response code="404">Resource was not found</response>
    /// <response code="400">Validation error occurred</response>
    public IActionResult SampleMethodWithResponses(int id) => throw new NotImplementedException();

    [Fact]
    public void GetResponseDescription_Returns200Description_WhenXmlExists()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(SampleMethodWithResponses));

        var desc = XmlDocumentationHelper.GetResponseDescription(method!, 200);

        Assert.NotNull(desc);
        Assert.Contains("succeeded", desc);
    }

    [Fact]
    public void GetResponseDescription_Returns404Description_WhenXmlExists()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(SampleMethodWithResponses));

        var desc = XmlDocumentationHelper.GetResponseDescription(method!, 404);

        Assert.NotNull(desc);
        Assert.Contains("not found", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetResponseDescription_Returns400Description_WhenXmlExists()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(SampleMethodWithResponses));

        var desc = XmlDocumentationHelper.GetResponseDescription(method!, 400);

        Assert.NotNull(desc);
        Assert.Contains("Validation", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetResponseDescription_ReturnsNull_WhenStatusCodeNotDocumented()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(SampleMethodWithResponses));

        var desc = XmlDocumentationHelper.GetResponseDescription(method!, 500);

        Assert.Null(desc);
    }

    [Fact]
    public void GetResponseDescription_ReturnsNull_WhenMethodHasNoXmlDoc()
    {
        // SampleMethod has no <response> tags
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(SampleMethod));

        var desc = XmlDocumentationHelper.GetResponseDescription(method!, 200);

        Assert.Null(desc);
    }

    // -------------------------------------------------------------------------
    // GetPropertySummary
    // -------------------------------------------------------------------------

    /// <summary>A documented property for testing.</summary>
    public string DocumentedProperty { get; set; } = string.Empty;

    [Fact]
    public void GetPropertySummary_ReturnsDocumentation_WhenXmlExists()
    {
        var property = typeof(XmlDocumentationHelperTests).GetProperty(nameof(DocumentedProperty))!;

        var summary = XmlDocumentationHelper.GetPropertySummary(property);

        Assert.NotNull(summary);
        Assert.Contains("documented property", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPropertySummary_ReturnsNull_WhenNoDocumentation()
    {
        // System assemblies have no XML docs loaded by our helper
        var systemProperty = typeof(Exception).GetProperty(nameof(Exception.Message))!;
        Assert.Null(XmlDocumentationHelper.GetPropertySummary(systemProperty));
    }

    // -------------------------------------------------------------------------
    // Null / missing-doc edge cases
    // -------------------------------------------------------------------------

    /// <summary>Method without a returns tag.</summary>
    private void MethodWithNoReturnsTag() { }

    [Fact]
    public void GetReturnsDescription_ReturnsNull_WhenMethodHasNoReturnsTag()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(MethodWithNoReturnsTag), BindingFlags.NonPublic | BindingFlags.Instance)!;

        var result = XmlDocumentationHelper.GetReturnsDescription(method);

        Assert.Null(result);
    }

    private void MethodWithNoXmlDoc(int x) { }

    [Fact]
    public void GetParameterDescription_ReturnsNull_WhenMethodHasNoXmlDoc()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(MethodWithNoXmlDoc), BindingFlags.NonPublic | BindingFlags.Instance)!;

        var result = XmlDocumentationHelper.GetParameterDescription(method, "x");

        Assert.Null(result);
    }

    /// <summary>Method with only a summary, no param tags.</summary>
    private void MethodWithSummaryButNoParams(int value) { }

    [Fact]
    public void GetParameterDescription_ReturnsNull_WhenParamNotDocumented()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(MethodWithSummaryButNoParams), BindingFlags.NonPublic | BindingFlags.Instance)!;

        var result = XmlDocumentationHelper.GetParameterDescription(method, "value");

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Generic / array parameter member-name resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Method with a generic parameter for XML member-name resolution.
    /// </summary>
    /// <param name="items">A list of items.</param>
    private void MethodWithGenericParam(List<string> items) { }

    [Fact]
    public void GetMethodSummary_ReturnsDocumentation_ForMethodWithGenericParam()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(MethodWithGenericParam), BindingFlags.NonPublic | BindingFlags.Instance)!;

        var summary = XmlDocumentationHelper.GetMethodSummary(method);

        Assert.NotNull(summary);
        Assert.Contains("generic parameter", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetParameterDescription_ReturnsDocumentation_ForGenericParam()
    {
        var method = typeof(XmlDocumentationHelperTests).GetMethod(nameof(MethodWithGenericParam), BindingFlags.NonPublic | BindingFlags.Instance)!;

        var desc = XmlDocumentationHelper.GetParameterDescription(method, "items");

        Assert.NotNull(desc);
        Assert.Contains("list", desc, StringComparison.OrdinalIgnoreCase);
    }
}
