using System.Reflection;
using Kaya.ApiExplorer.Helpers;
using Kaya.ApiExplorer.Models;

namespace Kaya.ApiExplorer.Tests;

public class ReflectionHelperTests
{
    [Theory]
    [InlineData(typeof(string), "string")]
    [InlineData(typeof(int), "integer")]
    [InlineData(typeof(bool), "boolean")]
    [InlineData(typeof(DateTime), "datetime")]
    [InlineData(typeof(Guid), "guid")]
    [InlineData(typeof(decimal), "decimal")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(void), "void")]
    [InlineData(typeof(object), "object")]
    public void GetFriendlyTypeName_ShouldReturnCorrectNames(Type type, string expected)
    {
        // Act
        var result = ReflectionHelper.GetFriendlyTypeName(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetFriendlyTypeName_ShouldHandleNullableTypes()
    {
        // Act
        var result = ReflectionHelper.GetFriendlyTypeName(typeof(int?));

        // Assert
        Assert.Equal("integer?", result);
    }

    [Fact]
    public void GetFriendlyTypeName_ShouldHandleArrays()
    {
        // Act
        var result = ReflectionHelper.GetFriendlyTypeName(typeof(List<string>));

        // Assert
        Assert.Equal("string[]", result);
    }

    [Fact]
    public void GetFriendlyTypeName_ShouldHandleDictionary()
    {
        // Act
        var result = ReflectionHelper.GetFriendlyTypeName(typeof(Dictionary<string, int>));

        // Assert
        Assert.Equal("Dictionary<string, integer>", result);
    }

    [Fact]
    public void IsComplexType_ShouldReturnTrueForCustomClasses()
    {
        // Act
        var result = ReflectionHelper.IsComplexType(typeof(TestUser));

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(Guid))]
    public void IsComplexType_ShouldReturnFalseForPrimitiveTypes(Type type)
    {
        // Act
        var result = ReflectionHelper.IsComplexType(type);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsComplexType_ShouldReturnFalseForDictionary()
    {
        // Act
        var result = ReflectionHelper.IsComplexType(typeof(Dictionary<string, int>));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSystemAssembly_ShouldReturnTrueForSystemAssemblies()
    {
        // Arrange
        var systemAssembly = typeof(string).Assembly;

        // Act
        var result = ReflectionHelper.IsSystemAssembly(systemAssembly);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSystemAssembly_ShouldReturnFalseForUserAssemblies()
    {
        // Arrange
        var userAssembly = Assembly.GetExecutingAssembly();

        // Act
        var result = ReflectionHelper.IsSystemAssembly(userAssembly);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CombineRoutes_ShouldCombineCorrectly()
    {
        // Act
        var result = ReflectionHelper.CombineRoutes("api/users", "profile");

        // Assert
        Assert.Equal("/api/users/profile", result);
    }

    [Fact]
    public void CombineRoutes_ShouldHandleEmptyAdditionalRoute()
    {
        // Act
        var result = ReflectionHelper.CombineRoutes("api/users", "");

        // Assert
        Assert.Equal("/api/users", result);
    }

    [Fact]
    public void CombineRoutes_ShouldHandleAbsoluteAdditionalRoute()
    {
        // Act
        var result = ReflectionHelper.CombineRoutes("api/users", "/absolute/path");

        // Assert
        Assert.Equal("/absolute/path", result);
    }

    [Fact]
    public void GenerateExampleJson_ShouldGenerateForPrimitiveTypes()
    {
        // Arrange
        var schemas = new Dictionary<string, ApiSchema>();
        var processedTypes = new HashSet<Type>();

        // Act
        var stringResult = ReflectionHelper.GenerateExampleJson(typeof(string), schemas, processedTypes);
        var intResult = ReflectionHelper.GenerateExampleJson(typeof(int), schemas, processedTypes);
        var boolResult = ReflectionHelper.GenerateExampleJson(typeof(bool), schemas, processedTypes);

        // Assert
        Assert.Equal("\"string value\"", stringResult);
        Assert.Equal("123", intResult);
        Assert.Equal("true", boolResult);
    }

    [Fact]
    public void GenerateExampleJson_ShouldGenerateForComplexType()
    {
        // Arrange
        var schemas = new Dictionary<string, ApiSchema>();
        var processedTypes = new HashSet<Type>();

        // Act
        var result = ReflectionHelper.GenerateExampleJson(typeof(TestUser), schemas, processedTypes);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("name", result.ToLower());
    }

    [Fact]
    public void GenerateSchemaForType_ShouldGenerateSchemaForComplexType()
    {
        // Act
        var schema = ReflectionHelper.GenerateSchemaForType(typeof(TestUser));

        // Assert
        Assert.NotNull(schema);
        Assert.Equal("object", schema.Type);
        Assert.NotEmpty(schema.Example);
    }

    // -------------------------------------------------------------------------
    // IsEnumerableType
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(typeof(List<int>), true)]
    [InlineData(typeof(IEnumerable<string>), true)]
    [InlineData(typeof(ICollection<int>), true)]
    [InlineData(typeof(IList<int>), true)]
    [InlineData(typeof(IReadOnlyCollection<int>), true)]
    [InlineData(typeof(IReadOnlyList<int>), true)]
    [InlineData(typeof(int[]), true)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    public void IsEnumerableType_ShouldReturnCorrectValue(Type type, bool expected)
    {
        var result = ReflectionHelper.IsEnumerableType(type);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsEnumerableType_ShouldReturnFalseForComplexType()
    {
        Assert.False(ReflectionHelper.IsEnumerableType(typeof(TestUser)));
    }

    // -------------------------------------------------------------------------
    // GetFriendlyTypeName – additional cases
    // -------------------------------------------------------------------------

    [Fact]
    public void GetFriendlyTypeName_ShouldHandleNullableBool()
    {
        Assert.Equal("boolean?", ReflectionHelper.GetFriendlyTypeName(typeof(bool?)));
    }

    [Fact]
    public void GetFriendlyTypeName_ShouldHandleNullableGuid()
    {
        Assert.Equal("guid?", ReflectionHelper.GetFriendlyTypeName(typeof(Guid?)));
    }

    [Fact]
    public void GetFriendlyTypeName_ShouldHandleIEnumerableOfInt()
    {
        Assert.Equal("integer[]", ReflectionHelper.GetFriendlyTypeName(typeof(IEnumerable<int>)));
    }

    [Fact]
    public void GetFriendlyTypeName_ShouldHandleByteArray()
    {
        Assert.Equal("byte", ReflectionHelper.GetFriendlyTypeName(typeof(byte[])));
    }

    // -------------------------------------------------------------------------
    // IsComplexType – additional cases
    // -------------------------------------------------------------------------

    [Fact]
    public void IsComplexType_ShouldReturnFalseForEnum()
    {
        Assert.False(ReflectionHelper.IsComplexType(typeof(TestStatus)));
    }

    [Fact]
    public void IsComplexType_ShouldReturnFalseForNullableInt()
    {
        Assert.False(ReflectionHelper.IsComplexType(typeof(int?)));
    }

    // -------------------------------------------------------------------------
    // GenerateExampleJson – additional primitive/enum/collection/dict cases
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateExampleJson_ShouldHandleDateTime()
    {
        var result = ReflectionHelper.GenerateExampleJson(typeof(DateTime), new(), new());
        Assert.Contains("2023", result);
    }

    [Fact]
    public void GenerateExampleJson_ShouldHandleGuid()
    {
        var result = ReflectionHelper.GenerateExampleJson(typeof(Guid), new(), new());
        Assert.Contains("3fa85f64", result);
    }

    [Fact]
    public void GenerateExampleJson_ShouldHandleDecimal()
    {
        var result = ReflectionHelper.GenerateExampleJson(typeof(decimal), new(), new());
        Assert.Equal("12.34", result);
    }

    [Fact]
    public void GenerateExampleJson_ShouldHandleByte()
    {
        var result = ReflectionHelper.GenerateExampleJson(typeof(byte), new(), new());
        Assert.Equal("0", result);
    }

    [Fact]
    public void GenerateExampleJson_ShouldHandleEnum()
    {
        var result = ReflectionHelper.GenerateExampleJson(typeof(TestStatus), new(), new());
        Assert.Equal("0", result);
    }

    [Fact]
    public void GenerateExampleJson_ShouldHandleDictionary()
    {
        var result = ReflectionHelper.GenerateExampleJson(typeof(Dictionary<string, int>), new(), new());
        Assert.Contains("key1", result);
    }

    [Fact]
    public void GenerateExampleJson_ShouldHandleList()
    {
        var result = ReflectionHelper.GenerateExampleJson(typeof(List<string>), new(), new());
        Assert.StartsWith("[", result);
        Assert.Contains("string value", result);
    }

    // -------------------------------------------------------------------------
    // GenerateSchemaForType – nested complex type
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateSchemaForType_ShouldHandleNestedComplexType()
    {
        var schema = ReflectionHelper.GenerateSchemaForType(typeof(UserWithAddress));
        Assert.NotNull(schema);
        Assert.Contains("Address", schema.Properties.Keys, StringComparer.OrdinalIgnoreCase);
    }

    public class PostalAddress
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    public class UserWithAddress
    {
        public string Name { get; set; } = string.Empty;
        public PostalAddress Address { get; set; } = new();
    }

    // -------------------------------------------------------------------------
    // GetPropertyConstraints
    // -------------------------------------------------------------------------

    private class ConstrainedModel
    {
        [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 2)]
        public string? NameWithStringLength { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0.0, 100.0)]
        public double Score { get; set; }

        [System.ComponentModel.DataAnnotations.RegularExpression(@"^\d{5}$")]
        public string? Zip { get; set; }

        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string? Email { get; set; }

        [System.ComponentModel.DataAnnotations.Url]
        public string? Website { get; set; }

        [System.ComponentModel.DataAnnotations.Phone]
        public string? PhoneNumber { get; set; }

        [System.ComponentModel.DataAnnotations.CreditCard]
        public string? CardNumber { get; set; }

        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public string? MinOnly { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? MaxOnly { get; set; }

        public string? Unconstrained { get; set; }
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsNull_WhenNoAttributes()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.Unconstrained))!;
        Assert.Null(ReflectionHelper.GetPropertyConstraints(property));
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithStringLength()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.NameWithStringLength))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal(50, result.MaxLength);
        Assert.Equal(2, result.MinLength);
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithRange()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.Score))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal(0.0, result.Minimum);
        Assert.Equal(100.0, result.Maximum);
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithRegex()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.Zip))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal(@"^\d{5}$", result.Pattern);
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithEmailFormat()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.Email))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal("email", result.Format);
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithUrlFormat()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.Website))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal("url", result.Format);
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithPhoneFormat()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.PhoneNumber))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal("phone", result.Format);
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithCreditCardFormat()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.CardNumber))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal("credit-card", result.Format);
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithMinLength()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.MinOnly))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal(1, result.MinLength);
    }

    [Fact]
    public void GetPropertyConstraints_ReturnsConstraints_WithMaxLength()
    {
        var property = typeof(ConstrainedModel).GetProperty(nameof(ConstrainedModel.MaxOnly))!;
        var result = ReflectionHelper.GetPropertyConstraints(property);
        Assert.NotNull(result);
        Assert.Equal(100, result.MaxLength);
    }

    // -------------------------------------------------------------------------
    // GetParameterConstraints
    // -------------------------------------------------------------------------

    private static void HelperMethodWithPlainParam(string value) { }

    [Fact]
    public void GetParameterConstraints_ReturnsNull_WhenNoAttributes()
    {
        var method = typeof(ReflectionHelperTests).GetMethod(nameof(HelperMethodWithPlainParam), BindingFlags.NonPublic | BindingFlags.Static)!;
        var param = method.GetParameters()[0];
        Assert.Null(ReflectionHelper.GetParameterConstraints(param));
    }

    // -------------------------------------------------------------------------
    // CombineRoutes – additional edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void CombineRoutes_ShouldHandleLeadingSlashOnBase()
    {
        var result = ReflectionHelper.CombineRoutes("/api/users", "profile");
        Assert.Equal("/api/users/profile", result);
    }

    [Fact]
    public void CombineRoutes_ShouldHandleLeadingSlashOnBoth()
    {
        var result = ReflectionHelper.CombineRoutes("/api/users", "/absolute");
        Assert.Equal("/absolute", result);
    }
}
