using System.Text.Json;
using Kaya.ApiExplorer.Models;
using Kaya.ApiExplorer.Services;

namespace Kaya.ApiExplorer.Tests;

public class OpenApiExportServiceTests
{
    private static readonly OpenApiExportService Service = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ApiDocumentation BuildDoc(params ApiEndpoint[] endpoints)
        => new()
        {
            Title = "Test API",
            Version = "1.0.0",
            Controllers =
            [
                new ApiController { Name = "TestController", Endpoints = [..endpoints] }
            ]
        };

    private static Dictionary<string, object> JsonRoundTrip(object spec)
    {
        var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
    }

    // Extract the responses dict for the first endpoint of the first tag in paths
    private static Dictionary<string, JsonElement> GetResponses(object spec, string path, string method)
    {
        var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var responses = doc.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty(method)
            .GetProperty("responses");

        return responses.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    // ── Spec structure ────────────────────────────────────────────────────────

    [Fact]
    public void GenerateOpenApiSpec_ContainsRequiredTopLevelKeys()
    {
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDoc()));

        Assert.True(spec.ContainsKey("openapi"));
        Assert.True(spec.ContainsKey("info"));
        Assert.True(spec.ContainsKey("paths"));
    }

    [Fact]
    public void GenerateOpenApiSpec_OpenApiVersion_Is_3_0_3()
    {
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDoc()));

        Assert.Equal("3.0.3", ((JsonElement)spec["openapi"]).GetString());
    }

    [Fact]
    public void GenerateOpenApiSpec_InfoContainsTitleAndVersion()
    {
        var documentation = new ApiDocumentation { Title = "My API", Version = "2.5.0", Controllers = [] };
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(documentation));

        var info = (JsonElement)spec["info"];
        Assert.Equal("My API", info.GetProperty("title").GetString());
        Assert.Equal("2.5.0", info.GetProperty("version").GetString());
    }

    // ── BuildResponses — ProducesResponses takes priority ─────────────────────

    [Fact]
    public void BuildResponses_UsesProducesResponses_WhenPresent()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "GET",
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 200, Type = "TestUser", Description = "OK" },
                new ApiProducesResponse { StatusCode = 404, Type = string.Empty, Description = "Not Found" }
            ]
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "get");

        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("404"));
        Assert.False(responses.ContainsKey("201"), "Should not add a 201 that wasn't declared");
    }

    [Fact]
    public void BuildResponses_200HasContentSchema_WhenTypeIsSet()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "GET",
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 200, Type = "string", Description = "OK" }
            ]
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "get");

        var ok = responses["200"];
        Assert.True(ok.TryGetProperty("content", out _), "200 with a type should include content");
    }

    [Fact]
    public void BuildResponses_NoBody_WhenTypeIsEmpty()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "DELETE",
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 204, Type = string.Empty, Description = "No Content" }
            ]
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "delete");

        var noContent = responses["204"];
        Assert.False(noContent.TryGetProperty("content", out _), "204 with no type should have no content");
    }

    [Fact]
    public void BuildResponses_DescriptionSet_FromProducesResponse()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "GET",
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 200, Type = string.Empty, Description = "Everything went fine" }
            ]
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "get");

        Assert.Equal("Everything went fine", responses["200"].GetProperty("description").GetString());
    }

    // ── BuildResponses — fallback to Response ─────────────────────────────────

    [Fact]
    public void BuildResponses_FallsBackTo200_WhenNoProducesResponses_AndResponseSet()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "GET",
            Response = new ApiResponse { Type = "string", Description = "Plain success" }
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "get");

        Assert.True(responses.ContainsKey("200"));
        Assert.Equal("Plain success", responses["200"].GetProperty("description").GetString());
    }

    [Fact]
    public void BuildResponses_FallsBackToSuccessDescription_WhenResponseDescriptionEmpty()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "GET",
            Response = new ApiResponse { Type = "string", Description = string.Empty }
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "get");

        Assert.Equal("Success", responses["200"].GetProperty("description").GetString());
    }

    [Fact]
    public void BuildResponses_Returns200Success_WhenNoResponseAndNoProduces()
    {
        var endpoint = new ApiEndpoint { Path = "/api/test", HttpMethodType = "POST" };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "post");

        Assert.True(responses.ContainsKey("200"));
        Assert.Equal("Success", responses["200"].GetProperty("description").GetString());
    }

    // ── Auth responses ────────────────────────────────────────────────────────

    [Fact]
    public void BuildResponses_Adds401And403_WhenEndpointRequiresAuth()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/secure",
            HttpMethodType = "GET",
            RequiresAuthorization = true
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/secure", "get");

        Assert.True(responses.ContainsKey("401"));
        Assert.True(responses.ContainsKey("403"));
    }

    [Fact]
    public void BuildResponses_AuthDoesNotOverwrite_ExplicitProducesAuth()
    {
        // If developer explicitly declared 401 with a body type, that should be kept
        var endpoint = new ApiEndpoint
        {
            Path = "/api/secure",
            HttpMethodType = "GET",
            RequiresAuthorization = true,
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 200, Type = "string", Description = "OK" },
                new ApiProducesResponse { StatusCode = 401, Type = "string", Description = "Custom unauthorized message" }
            ]
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/secure", "get");

        Assert.Equal("Custom unauthorized message", responses["401"].GetProperty("description").GetString());
    }

    [Fact]
    public void BuildResponses_NoAuthResponses_WhenNotRequired()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/public",
            HttpMethodType = "GET",
            RequiresAuthorization = false
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/public", "get");

        Assert.False(responses.ContainsKey("401"));
        Assert.False(responses.ContainsKey("403"));
    }

    // ── Security scheme ───────────────────────────────────────────────────────

    [Fact]
    public void GenerateOpenApiSpec_IncludesBearerAuthScheme_WhenAnyEndpointRequiresAuth()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/secure",
            HttpMethodType = "GET",
            RequiresAuthorization = true
        };
        var doc = BuildDoc(endpoint);
        doc.Controllers[0].RequiresAuthorization = true;

        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(doc));

        var components = (JsonElement)spec["components"];
        Assert.True(components.TryGetProperty("securitySchemes", out var schemes));
        Assert.True(schemes.TryGetProperty("bearerAuth", out _));
    }

    [Fact]
    public void GenerateOpenApiSpec_NoBearerAuth_WhenNoEndpointRequiresAuth()
    {
        var endpoint = new ApiEndpoint { Path = "/api/open", HttpMethodType = "GET" };

        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDoc(endpoint)));

        if (spec.TryGetValue("components", out var compsObj))
        {
            var comps = (JsonElement)compsObj;
            Assert.False(comps.TryGetProperty("securitySchemes", out _));
        }
        // If there's no components key at all that's fine too
    }

    // ── Schema registration via ProducesResponses ─────────────────────────────

    [Fact]
    public void GenerateOpenApiSpec_RegistersSchema_FromProducesResponseType()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "GET",
            ProducesResponses =
            [
                new ApiProducesResponse
                {
                    StatusCode = 200,
                    Type = "TestUser",
                    Description = "OK",
                    Schema = new ApiSchema
                    {
                        Type = "TestUser",
                        Properties = new Dictionary<string, ApiProperty>
                        {
                            ["Id"]   = new ApiProperty { Type = "integer" },
                            ["Name"] = new ApiProperty { Type = "string"  }
                        }
                    }
                }
            ]
        };

        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDoc(endpoint)));

        // The type should appear as a $ref in the 200 content and the schema
        // should land in components/schemas
        Assert.True(spec.ContainsKey("components"));
        var components = (JsonElement)spec["components"];
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        Assert.True(schemas.TryGetProperty("TestUser", out _));
    }

    // ── Path / operation structure ────────────────────────────────────────────

    [Fact]
    public void GenerateOpenApiSpec_PathContainsCorrectHttpMethod()
    {
        var endpoint = new ApiEndpoint { Path = "/api/items", HttpMethodType = "POST" };

        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDoc(endpoint)));

        var paths = (JsonElement)spec["paths"];
        Assert.True(paths.TryGetProperty("/api/items", out var pathItem));
        Assert.True(pathItem.TryGetProperty("post", out _));
    }

    [Fact]
    public void GenerateOpenApiSpec_OperationContainsTag_FromControllerName()
    {
        var endpoint = new ApiEndpoint { Path = "/api/test", HttpMethodType = "GET" };

        var json = JsonSerializer.Serialize(Service.GenerateOpenApiSpec(BuildDoc(endpoint)),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);

        var tags = doc.RootElement
            .GetProperty("paths")
            .GetProperty("/api/test")
            .GetProperty("get")
            .GetProperty("tags");

        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        // Controller is "TestController" → tag should be "Test"
        Assert.Contains(tags.EnumerateArray(), t => t.GetString() == "Test");
    }

    [Fact]
    public void GenerateOpenApiSpec_DeprecatedTrue_WhenEndpointIsObsolete()
    {
        var endpoint = new ApiEndpoint { Path = "/api/old", HttpMethodType = "GET", IsObsolete = true };

        var json = JsonSerializer.Serialize(Service.GenerateOpenApiSpec(BuildDoc(endpoint)),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);

        var deprecated = doc.RootElement
            .GetProperty("paths")
            .GetProperty("/api/old")
            .GetProperty("get")
            .GetProperty("deprecated");

        Assert.True(deprecated.GetBoolean());
    }

    // ── Multiple ProducesResponses with different status codes ────────────────

    [Fact]
    public void BuildResponses_AllDeclaredStatusCodes_PresentInOutput()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/orders",
            HttpMethodType = "POST",
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 201, Type = "string", Description = "Created" },
                new ApiProducesResponse { StatusCode = 400, Type = string.Empty, Description = "Bad Request" },
                new ApiProducesResponse { StatusCode = 401, Type = string.Empty, Description = "Unauthorized" },
                new ApiProducesResponse { StatusCode = 422, Type = string.Empty, Description = "Unprocessable Entity" }
            ]
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/orders", "post");

        Assert.True(responses.ContainsKey("201"));
        Assert.True(responses.ContainsKey("400"));
        Assert.True(responses.ContainsKey("401"));
        Assert.True(responses.ContainsKey("422"));
    }

    [Fact]
    public void BuildResponses_FallbackResponseNotUsed_WhenProducesResponsesPresent()
    {
        // Even when Response is set, ProducesResponses should win and there should
        // be no duplicate/extra 200 added from the Response fallback path.
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "GET",
            Response = new ApiResponse { Type = "string", Description = "Should be ignored" },
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 204, Type = string.Empty, Description = "No Content" }
            ]
        };

        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "get");

        Assert.True(responses.ContainsKey("204"));
        Assert.False(responses.ContainsKey("200"), "Response fallback must not add a 200 when ProducesResponses is used");
    }

    // -------------------------------------------------------------------------
    // Spec-level metadata: contact, license, servers, termsOfService
    // -------------------------------------------------------------------------

    private static ApiDocumentation BuildDocWithMetadata() => new()
    {
        Title = "Meta API",
        Version = "3.0.0",
        Description = "Desc",
        TermsOfService = "https://example.com/terms",
        Contact = new ApiDocumentationContact { Name = "Jane", Email = "jane@example.com", Url = "https://jane.dev" },
        License = new ApiDocumentationLicense { Name = "MIT", Url = "https://opensource.org/licenses/MIT" },
        Servers = [new ApiDocumentationServer { Url = "https://api.example.com", Description = "Prod" }],
        Controllers = []
    };

    [Fact]
    public void GenerateOpenApiSpec_IncludesContact_WhenSet()
    {
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDocWithMetadata()));
        var info = (JsonElement)spec["info"];
        Assert.True(info.TryGetProperty("contact", out var contact));
        Assert.Equal("Jane", contact.GetProperty("name").GetString());
        Assert.Equal("jane@example.com", contact.GetProperty("email").GetString());
        Assert.Equal("https://jane.dev", contact.GetProperty("url").GetString());
    }

    [Fact]
    public void GenerateOpenApiSpec_IncludesLicense_WhenSet()
    {
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDocWithMetadata()));
        var info = (JsonElement)spec["info"];
        Assert.True(info.TryGetProperty("license", out var license));
        Assert.Equal("MIT", license.GetProperty("name").GetString());
        Assert.Equal("https://opensource.org/licenses/MIT", license.GetProperty("url").GetString());
    }

    [Fact]
    public void GenerateOpenApiSpec_IncludesServers_WhenSet()
    {
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDocWithMetadata()));
        Assert.True(spec.ContainsKey("servers"));
        var servers = (JsonElement)spec["servers"];
        Assert.Equal(JsonValueKind.Array, servers.ValueKind);
        Assert.Equal("https://api.example.com", servers[0].GetProperty("url").GetString());
    }

    [Fact]
    public void GenerateOpenApiSpec_IncludesTermsOfService_WhenSet()
    {
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDocWithMetadata()));
        var info = (JsonElement)spec["info"];
        Assert.True(info.TryGetProperty("termsOfService", out var tos));
        Assert.Equal("https://example.com/terms", tos.GetString());
    }

    [Fact]
    public void GenerateOpenApiSpec_Contact_OmittedWhenNotSet()
    {
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDoc()));
        var info = (JsonElement)spec["info"];
        Assert.False(info.TryGetProperty("contact", out _));
    }

    [Fact]
    public void GenerateOpenApiSpec_License_OmittedWhenNotSet()
    {
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDoc()));
        var info = (JsonElement)spec["info"];
        Assert.False(info.TryGetProperty("license", out _));
    }

    // -------------------------------------------------------------------------
    // Parameter building: route / query / header
    // -------------------------------------------------------------------------

    private static List<JsonElement> GetOperationParameters(object spec, string path, string method)
    {
        var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var operation = doc.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        if (!operation.TryGetProperty("parameters", out var parameters))
            return [];
        return [.. parameters.EnumerateArray().Select(p => p.Clone())];
    }

    [Fact]
    public void BuildParameters_RouteParameter_HasPathIn()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/items/{id}",
            HttpMethodType = "GET",
            Parameters = [new ApiParameter { Name = "id", Type = "integer", Source = "Route", Required = true }]
        };
        var parameters = GetOperationParameters(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/items/{id}", "get");
        var idParam = parameters.First(p => p.GetProperty("name").GetString() == "id");
        Assert.Equal("path", idParam.GetProperty("in").GetString());
        Assert.True(idParam.GetProperty("required").GetBoolean());
    }

    [Fact]
    public void BuildParameters_QueryParameter_HasQueryIn()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/items",
            HttpMethodType = "GET",
            Parameters = [new ApiParameter { Name = "page", Type = "integer", Source = "Query", Required = false }]
        };
        var parameters = GetOperationParameters(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/items", "get");
        var pageParam = parameters.First(p => p.GetProperty("name").GetString() == "page");
        Assert.Equal("query", pageParam.GetProperty("in").GetString());
    }

    [Fact]
    public void BuildParameters_HeaderParameter_HasHeaderIn_AndUsesHeaderName()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/secure",
            HttpMethodType = "GET",
            Parameters =
            [
                new ApiParameter
                {
                    Name = "authorization",
                    HeaderName = "X-Api-Key",
                    Type = "string",
                    Source = "Header",
                    Required = true
                }
            ]
        };
        var parameters = GetOperationParameters(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/secure", "get");
        var headerParam = parameters.First();
        Assert.Equal("header", headerParam.GetProperty("in").GetString());
        Assert.Equal("X-Api-Key", headerParam.GetProperty("name").GetString());
    }

    [Fact]
    public void BuildParameters_SkipsBodyAndFileAndFormParams()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/upload",
            HttpMethodType = "POST",
            Parameters =
            [
                new ApiParameter { Name = "file", Type = "string", Source = "File", IsFile = true },
                new ApiParameter { Name = "data", Type = "string", Source = "Body" },
                new ApiParameter { Name = "note", Type = "string", Source = "Form" },
                new ApiParameter { Name = "tag", Type = "string", Source = "Query" }
            ]
        };
        var parameters = GetOperationParameters(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/upload", "post");
        Assert.Single(parameters);
        Assert.Equal("tag", parameters[0].GetProperty("name").GetString());
    }

    [Fact]
    public void BuildParameters_AppliesConstraints_ToSchema()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/items",
            HttpMethodType = "GET",
            Parameters =
            [
                new ApiParameter
                {
                    Name = "q",
                    Type = "string",
                    Source = "Query",
                    Required = false,
                    Constraints = new ApiConstraints { MinLength = 3, MaxLength = 50 }
                }
            ]
        };
        var parameters = GetOperationParameters(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/items", "get");
        var schema = parameters[0].GetProperty("schema");
        Assert.Equal(3, schema.GetProperty("minLength").GetInt32());
        Assert.Equal(50, schema.GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public void BuildParameters_DefaultValue_IncludedInSchema()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/items",
            HttpMethodType = "GET",
            Parameters =
            [
                new ApiParameter { Name = "page", Type = "integer", Source = "Query", DefaultValue = 1 }
            ]
        };
        var parameters = GetOperationParameters(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/items", "get");
        var schema = parameters[0].GetProperty("schema");
        Assert.Equal(1, schema.GetProperty("default").GetInt32());
    }

    // -------------------------------------------------------------------------
    // Schema type building for various friendly type names
    // -------------------------------------------------------------------------

    private Dictionary<string, JsonElement> GetSchemas(object spec)
    {
        var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("components", out var components)) return [];
        if (!components.TryGetProperty("schemas", out var schemas)) return [];
        return schemas.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    [Theory]
    [InlineData("integer", "integer", "int32")]
    [InlineData("boolean", "boolean", null)]
    [InlineData("datetime", "string", "date-time")]
    [InlineData("guid", "string", "uuid")]
    [InlineData("decimal", "number", "double")]
    [InlineData("double", "number", "double")]
    [InlineData("float", "number", "float")]
    [InlineData("byte", "string", "byte")]
    public void BuildSchemaFromFriendlyType_CorrectTypeAndFormat(string friendlyType, string expectedType, string? expectedFormat)
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/test",
            HttpMethodType = "GET",
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 200, Type = friendlyType, Description = "OK" }
            ]
        };
        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/test", "get");
        var schema = responses["200"].GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal(expectedType, schema.GetProperty("type").GetString());
        if (expectedFormat is not null)
            Assert.Equal(expectedFormat, schema.GetProperty("format").GetString());
    }

    [Fact]
    public void BuildSchemaFromFriendlyType_ArrayType_HasArrayTypeWithItems()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/items",
            HttpMethodType = "GET",
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 200, Type = "string[]", Description = "OK" }
            ]
        };
        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/items", "get");
        var schema = responses["200"].GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("array", schema.GetProperty("type").GetString());
        Assert.True(schema.TryGetProperty("items", out _));
    }

    [Fact]
    public void BuildSchemaFromFriendlyType_DictionaryType_HasObjectWithAdditionalProperties()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/dict",
            HttpMethodType = "GET",
            ProducesResponses =
            [
                new ApiProducesResponse { StatusCode = 200, Type = "Dictionary<string, integer>", Description = "OK" }
            ]
        };
        var responses = GetResponses(Service.GenerateOpenApiSpec(BuildDoc(endpoint)), "/api/dict", "get");
        var schema = responses["200"].GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.True(schema.TryGetProperty("additionalProperties", out _));
    }

    // -------------------------------------------------------------------------
    // File upload (multipart/form-data)
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildRequestBody_FileUpload_UsesMultipartFormData()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/upload",
            HttpMethodType = "POST",
            Parameters = [new ApiParameter { Name = "file", Type = "IFormFile", Source = "File", IsFile = true }]
        };
        var json = JsonSerializer.Serialize(
            Service.GenerateOpenApiSpec(BuildDoc(endpoint)),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var body = doc.RootElement.GetProperty("paths").GetProperty("/api/upload").GetProperty("post").GetProperty("requestBody");
        Assert.True(body.GetProperty("content").TryGetProperty("multipart/form-data", out _));
    }

    // -------------------------------------------------------------------------
    // Request body description
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildRequestBody_SetsDescription_WhenProvided()
    {
        var endpoint = new ApiEndpoint
        {
            Path = "/api/create",
            HttpMethodType = "POST",
            RequestBody = new ApiRequestBody { Type = "string", Description = "The payload", Example = string.Empty }
        };
        var json = JsonSerializer.Serialize(
            Service.GenerateOpenApiSpec(BuildDoc(endpoint)),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var body = doc.RootElement.GetProperty("paths").GetProperty("/api/create").GetProperty("post").GetProperty("requestBody");
        Assert.Equal("The payload", body.GetProperty("description").GetString());
    }

    // -------------------------------------------------------------------------
    // Tags list in spec
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateOpenApiSpec_TagsList_ContainsControllerName()
    {
        var endpoint = new ApiEndpoint { Path = "/api/test", HttpMethodType = "GET" };
        var spec = JsonRoundTrip(Service.GenerateOpenApiSpec(BuildDoc(endpoint)));
        Assert.True(spec.ContainsKey("tags"));
        var tags = (JsonElement)spec["tags"];
        Assert.Contains(tags.EnumerateArray(), t => t.GetProperty("name").GetString() == "Test");
    }
}
