using System.Text.RegularExpressions;
using Kaya.ApiExplorer.Models;

namespace Kaya.ApiExplorer.Services;

public interface IOpenApiExportService
{
    object GenerateOpenApiSpec(ApiDocumentation documentation);
}

public class OpenApiExportService : IOpenApiExportService
{
    // Well-known ASP.NET Core / BCL types that have fixed OpenAPI schema representations.
    private static readonly Dictionary<string, Dictionary<string, object>> WellKnownTypeSchemas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["IFormFile"] = new() { ["type"] = "string", ["format"] = "binary" },
            ["IFormCollection"] = new() { ["type"] = "object" },
            ["Stream"] = new() { ["type"] = "string", ["format"] = "binary" },
        };

    // Carries the schema registry (all known named schemas), the set of type names
    // that have been $ref-d so far, and the set of known enum type names.
    private sealed record SchemaCtx(
        Dictionary<string, ApiSchema> Registry,
        HashSet<string> Referenced,
        HashSet<string> EnumTypeNames);

    // -------------------------------------------------------------------------
    // Schema registry helpers
    // -------------------------------------------------------------------------

    private static void RegisterSchemas(string typeName, ApiSchema schema, Dictionary<string, ApiSchema> registry)
    {
        var rootName = SanitizeSchemaName(typeName);
        if (rootName.EndsWith("[]")) rootName = rootName[..^2];

        if (string.IsNullOrWhiteSpace(rootName) || registry.ContainsKey(rootName))
            return;

        registry[rootName] = schema;

        foreach (var (_, prop) in schema.Properties)
        {
            if (prop.NestedSchema is not null)
                RegisterSchemas(prop.Type, prop.NestedSchema, registry);
        }
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    // Converts a friendly type name to a valid OpenAPI component schema name.
    private static string SanitizeSchemaName(string typeName)
    {
        var name = typeName.TrimEnd('?');
        if (name.EndsWith("[]")) return name; // plain array types are unwrapped by the array schema builder
        // Replace innermost <...> groups iteratively (inside-out for nested generics)
        while (name.Contains('<'))
            name = Regex.Replace(name, @"<([^<>]+)>", m =>
            {
                // Convert array markers inside generic args: Foo[] → ListOfFoo
                var inner = Regex.Replace(m.Groups[1].Value, @"(\w+)\[\]", "ListOf$1");
                return "Of" + inner.Replace(", ", "And");
            });
        return name;
    }

    // Converts an ApiSchema into an OpenAPI schema object dict, emitting $refs for known complex types.
    private static Dictionary<string, object> BuildApiSchemaObject(ApiSchema schema, SchemaCtx ctx)
    {
        var result = new Dictionary<string, object> { ["type"] = "object" };

        if (schema.Properties.Count > 0)
        {
            var properties = new Dictionary<string, object>();
            foreach (var (propName, prop) in schema.Properties)
                properties[ToCamelCase(propName)] = BuildSchemaFromFriendlyType(prop.Type, ctx, prop.Constraints);
            result["properties"] = properties;
        }

        if (schema.Required.Count > 0)
            result["required"] = schema.Required.Select(ToCamelCase).ToList();

        return result;
    }

    public object GenerateOpenApiSpec(ApiDocumentation documentation)
    {
        // ── 1. Collect all named schemas from request bodies and responses ─────
        var registry = new Dictionary<string, ApiSchema>(StringComparer.OrdinalIgnoreCase);
        foreach (var controller in documentation.Controllers)
        foreach (var endpoint in controller.Endpoints)
        {
            if (endpoint.RequestBody?.Schema is not null)
                RegisterSchemas(endpoint.RequestBody.Type, endpoint.RequestBody.Schema, registry);
            if (endpoint.Response?.Schema is not null)
                RegisterSchemas(endpoint.Response.Type, endpoint.Response.Schema, registry);
        }

        var enumTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var controller in documentation.Controllers)
        foreach (var endpoint in controller.Endpoints)
        foreach (var param in endpoint.Parameters)
        {
            if (param.IsEnum)
                enumTypeNames.Add(param.Type.TrimEnd('?'));
        }

        var ctx = new SchemaCtx(registry, new HashSet<string>(StringComparer.OrdinalIgnoreCase), enumTypeNames);

        // ── 2. Build paths ────────────────────────────────────────────────────
        var paths = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var controller in documentation.Controllers)
        {
            foreach (var endpoint in controller.Endpoints)
            {
                var openApiPath = NormalizePathForOpenApi(endpoint.Path);

                if (!paths.TryGetValue(openApiPath, out var existingPathItem))
                {
                    existingPathItem = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    paths[openApiPath] = existingPathItem;
                }

                var pathItemDict = (Dictionary<string, object>)existingPathItem;
                var httpMethod = endpoint.HttpMethodType.ToLowerInvariant();

                pathItemDict[httpMethod] = BuildOperation(endpoint, controller.Name, ctx);
            }
        }

        var info = new Dictionary<string, object>
        {
            ["title"] = documentation.Title,
            ["version"] = documentation.Version,
            ["description"] = documentation.Description
        };

        if (!string.IsNullOrWhiteSpace(documentation.TermsOfService))
            info["termsOfService"] = documentation.TermsOfService;

        if (documentation.Contact is not null)
        {
            var contact = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(documentation.Contact.Name))  contact["name"]  = documentation.Contact.Name;
            if (!string.IsNullOrWhiteSpace(documentation.Contact.Email)) contact["email"] = documentation.Contact.Email;
            if (!string.IsNullOrWhiteSpace(documentation.Contact.Url))   contact["url"]   = documentation.Contact.Url;
            if (contact.Count > 0) info["contact"] = contact;
        }

        if (documentation.License is not null && !string.IsNullOrWhiteSpace(documentation.License.Name))
        {
            var license = new Dictionary<string, object> { ["name"] = documentation.License.Name };
            if (!string.IsNullOrWhiteSpace(documentation.License.Url)) license["url"] = documentation.License.Url!;
            info["license"] = license;
        }

        var spec = new Dictionary<string, object>
        {
            ["openapi"] = "3.0.3",
            ["info"] = info,
            ["paths"] = paths
        };

        if (documentation.Servers.Count > 0)
        {
            spec["servers"] = documentation.Servers.Select(s =>
            {
                var server = new Dictionary<string, object> { ["url"] = s.Url };
                if (!string.IsNullOrWhiteSpace(s.Description)) server["description"] = s.Description;
                return server;
            }).ToList<object>();
        }

        // Root-level tags (one entry per unique tag used in paths), sorted and placed after components
        var allTags = documentation.Controllers
            .Select(c => c.Name.Replace("Controller", ""))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(t => new Dictionary<string, object> { ["name"] = t })
            .ToList<object>();

        // ── 5. components ─────────────────────────────────────────────────────
        var components = new Dictionary<string, object>();

        // Build component schemas for every $ref-d type, iterating until stable
        // (building a component schema may reference further nested types)
        var componentSchemas = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var outstanding = ctx.Referenced.Except(processed, StringComparer.OrdinalIgnoreCase).ToList();
            if (outstanding.Count == 0) break;
            foreach (var typeName in outstanding)
            {
                processed.Add(typeName);
                if (ctx.EnumTypeNames.Contains(typeName))
                    componentSchemas[typeName] = new Dictionary<string, object> { ["type"] = "integer" };
                else if (WellKnownTypeSchemas.TryGetValue(typeName, out var wellKnown))
                    componentSchemas[typeName] = wellKnown;
                else if (ctx.Registry.TryGetValue(typeName, out var schema))
                    componentSchemas[typeName] = BuildApiSchemaObject(schema, ctx);
            }
        }

        if (componentSchemas.Count > 0)
            components["schemas"] = componentSchemas;

        // Add bearerAuth security scheme if any endpoint requires auth
        if (documentation.Controllers.Any(c => c.RequiresAuthorization || c.Endpoints.Any(e => e.RequiresAuthorization)))
        {
            components["securitySchemes"] = new Dictionary<string, object>
            {
                ["bearerAuth"] = new Dictionary<string, object>
                {
                    ["type"] = "http",
                    ["scheme"] = "bearer",
                    ["bearerFormat"] = "JWT"
                }
            };
        }

        if (components.Count > 0)
            spec["components"] = components;

        if (allTags.Count > 0)
            spec["tags"] = allTags;

        return spec;
    }

    // -------------------------------------------------------------------------
    // Operation builder
    // -------------------------------------------------------------------------

    private static Dictionary<string, object> BuildOperation(ApiEndpoint endpoint, string controllerName, SchemaCtx ctx)
    {
        var tag = controllerName.Replace("Controller", "");
        var operation = new Dictionary<string, object>
        {
            ["tags"] = new[] { tag },
            ["summary"] = endpoint.Description
        };

        if (endpoint.IsObsolete)
            operation["deprecated"] = true;

        // Parameters (Query / Route / Header — NOT Body / Form / File)
        var parameters = BuildParameters(endpoint, ctx);
        if (parameters.Count > 0)
            operation["parameters"] = parameters;

        // Request body
        var requestBody = BuildRequestBody(endpoint, ctx);
        if (requestBody is not null)
            operation["requestBody"] = requestBody;

        // Responses
        operation["responses"] = BuildResponses(endpoint, ctx);

        // Security
        if (endpoint.RequiresAuthorization)
            operation["security"] = new[] { new Dictionary<string, string[]> { ["bearerAuth"] = [] } };

        return operation;
    }

    // -------------------------------------------------------------------------
    // Parameters
    // -------------------------------------------------------------------------

    private static List<object> BuildParameters(ApiEndpoint endpoint, SchemaCtx ctx)
    {
        var result = new List<object>();

        foreach (var param in endpoint.Parameters)
        {
            if (param.Source is "Body" or "File" or "Form")
                continue;

            var paramIn = param.Source switch
            {
                "Route" => "path",
                "Header" => "header",
                _ => "query"
            };

            var paramObj = new Dictionary<string, object>
            {
                ["name"] = param.HeaderName ?? param.Name,
                ["in"] = paramIn,
                ["required"] = param.Source == "Route" || param.Required,
            };

            if (!string.IsNullOrWhiteSpace(param.Description))
                paramObj["description"] = param.Description;

            var schema = BuildSchemaFromFriendlyType(param.Type, ctx, param.Constraints);
            if (param.DefaultValue is not null)
                schema["default"] = param.DefaultValue;
            paramObj["schema"] = schema;

            result.Add(paramObj);
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Request body
    // -------------------------------------------------------------------------

    private static Dictionary<string, object>? BuildRequestBody(ApiEndpoint endpoint, SchemaCtx ctx)
    {
        var fileParams = endpoint.Parameters.Where(p => p.Source == "File" || p.IsFile).ToList();
        var formParams = endpoint.Parameters.Where(p => p.Source == "Form").ToList();

        if (fileParams.Count > 0 || formParams.Count > 0)
        {
            return BuildMultipartBody(fileParams, formParams, ctx);
        }

        if (endpoint.RequestBody is not null)
        {
            return BuildJsonBody(endpoint.RequestBody, ctx);
        }

        return null;
    }

    private static Dictionary<string, object> BuildJsonBody(ApiRequestBody requestBody, SchemaCtx ctx)
    {
        var schema = BuildSchemaFromFriendlyType(requestBody.Type, ctx);
        var mediaType = new Dictionary<string, object> { ["schema"] = schema };
        if (!string.IsNullOrWhiteSpace(requestBody.Example))
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<object>(requestBody.Example);
                if (parsed is not null) mediaType["example"] = parsed;
            }
            catch { /* ignore malformed example */ }
        }

        var schemaOnly = new Dictionary<string, object> { ["schema"] = schema };

        var body = new Dictionary<string, object> { ["required"] = true };

        if (!string.IsNullOrWhiteSpace(requestBody.Description))
            body["description"] = requestBody.Description;

        body["content"] = new Dictionary<string, object>
        {
            ["application/json"] = mediaType,
            ["text/json"] = schemaOnly,
            ["application/*+json"] = schemaOnly
        };

        return body;
    }

    private static Dictionary<string, object> BuildMultipartBody(
        List<ApiParameter> fileParams, List<ApiParameter> formParams, SchemaCtx ctx)
    {
        var properties = new Dictionary<string, object>();

        foreach (var fp in fileParams)
        {
            ctx.Referenced.Add("IFormFile");
            properties[fp.Name] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/IFormFile" };
        }

        foreach (var fp in formParams)
        {
            if (fp.Schema?.Properties is { Count: > 0 })
            {
                foreach (var (propName, propInfo) in fp.Schema.Properties)
                    properties[propName] = BuildSchemaFromFriendlyType(propInfo.Type, ctx, propInfo.Constraints);
            }
            else
            {
                properties[fp.Name] = BuildSchemaFromFriendlyType(fp.Type, ctx, fp.Constraints);
            }
        }

        return new Dictionary<string, object>
        {
            ["required"] = true,
            ["content"] = new Dictionary<string, object>
            {
                ["multipart/form-data"] = new Dictionary<string, object>
                {
                    ["schema"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = properties
                    }
                }
            }
        };
    }

    // -------------------------------------------------------------------------
    // Responses
    // -------------------------------------------------------------------------

    private static Dictionary<string, object> BuildResponses(ApiEndpoint endpoint, SchemaCtx ctx)
    {
        var responses = new Dictionary<string, object>();

        if (endpoint.Response is not null)
        {
            var schema = BuildSchemaFromFriendlyType(endpoint.Response.Type, ctx);
            var mediaTypeWithExample = new Dictionary<string, object> { ["schema"] = schema };
            var mediaTypeNoExample = new Dictionary<string, object> { ["schema"] = schema };

            if (!string.IsNullOrWhiteSpace(endpoint.Response.Example))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<object>(endpoint.Response.Example);
                    if (parsed is not null) mediaTypeWithExample["example"] = parsed;
                }
                catch { /* ignore */ }
            }

            var responseDescription = !string.IsNullOrWhiteSpace(endpoint.Response.Description)
                ? endpoint.Response.Description
                : "Success";

            responses["200"] = new Dictionary<string, object>
            {
                ["description"] = responseDescription,
                ["content"] = new Dictionary<string, object>
                {
                    ["text/plain"] = mediaTypeNoExample,
                    ["application/json"] = mediaTypeWithExample,
                    ["text/json"] = mediaTypeNoExample
                }
            };
        }
        else
        {
            responses["200"] = new Dictionary<string, object> { ["description"] = "Success" };
        }

        if (endpoint.RequiresAuthorization)
        {
            responses["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized" };
            responses["403"] = new Dictionary<string, object> { ["description"] = "Forbidden" };
        }

        return responses;
    }

    // -------------------------------------------------------------------------
    // Schema helpers
    // -------------------------------------------------------------------------

    private static Dictionary<string, object> BuildSchemaFromFriendlyType(
        string friendlyType, SchemaCtx ctx, ApiConstraints? constraints = null)
    {
        // Strip trailing '?' (nullable marker)
        var type = friendlyType.TrimEnd('?');

        Dictionary<string, object> schema = type switch
        {
            "string"             => new() { ["type"] = "string" },
            "integer" or "int"   => new() { ["type"] = "integer", ["format"] = "int32" },
            "boolean"            => new() { ["type"] = "boolean" },
            "datetime"           => new() { ["type"] = "string",  ["format"] = "date-time" },
            "guid"               => new() { ["type"] = "string",  ["format"] = "uuid" },
            "decimal"            => new() { ["type"] = "number",  ["format"] = "double" },
            "double"             => new() { ["type"] = "number",  ["format"] = "double" },
            "float"              => new() { ["type"] = "number",  ["format"] = "float" },
            "byte"               => new() { ["type"] = "string",  ["format"] = "byte" },
            "object"             => new() { ["type"] = "object" },
            _                    => BuildComplexOrArraySchema(type, ctx)
        };

        if (constraints is not null)
            ApplyConstraintsToSchema(schema, constraints);

        return schema;
    }

    private static Dictionary<string, object> BuildComplexOrArraySchema(string type, SchemaCtx ctx)
    {
        // Array types: "string[]", "integer[]", "UserDto[]", etc.
        if (type.EndsWith("[]"))
        {
            var elementType = type[..^2];
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = BuildSchemaFromFriendlyType(elementType, ctx)
            };
        }

        // Dictionary types: "Dictionary<string, integer>", etc.
        var dictMatch = Regex.Match(type, @"^Dictionary<(.+),\s*(.+)>$");
        if (dictMatch.Success)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["additionalProperties"] = BuildSchemaFromFriendlyType(dictMatch.Groups[2].Value.Trim(), ctx)
            };
        }

        // Known enum type → emit $ref and record for components/schemas
        var schemaName = SanitizeSchemaName(type);

        // Well-known framework / ASP.NET Core types that map to fixed schemas
        if (WellKnownTypeSchemas.ContainsKey(schemaName))
        {
            ctx.Referenced.Add(schemaName);
            return new Dictionary<string, object> { ["$ref"] = $"#/components/schemas/{schemaName}" };
        }

        if (ctx.EnumTypeNames.Contains(schemaName))
        {
            ctx.Referenced.Add(schemaName);
            return new Dictionary<string, object> { ["$ref"] = $"#/components/schemas/{schemaName}" };
        }

        // Named complex type in the registry → emit $ref and record for components/schemas
        if (ctx.Registry.ContainsKey(schemaName))
        {
            ctx.Referenced.Add(schemaName);
            return new Dictionary<string, object> { ["$ref"] = $"#/components/schemas/{schemaName}" };
        }

        // Unknown complex types — emit an object schema
        return new Dictionary<string, object> { ["type"] = "object" };
    }

    private static void ApplyConstraintsToSchema(Dictionary<string, object> schema, ApiConstraints c)
    {
        if (c.MinLength is not null)  schema["minLength"] = c.MinLength;
        if (c.MaxLength is not null)  schema["maxLength"] = c.MaxLength;
        if (c.Minimum is not null)    schema["minimum"]   = c.Minimum;
        if (c.Maximum is not null)    schema["maximum"]   = c.Maximum;
        if (c.Pattern is not null)    schema["pattern"]   = c.Pattern;
        if (c.Format is not null)     schema["format"]    = c.Format;
    }

    // -------------------------------------------------------------------------
    // Path normalisation
    // -------------------------------------------------------------------------

    private static string NormalizePathForOpenApi(string path)
    {
        // Route constraints like {id:int} → {id}
        return Regex.Replace(path, @"\{(\w+):[^}]+\}", "{$1}");
    }
}
