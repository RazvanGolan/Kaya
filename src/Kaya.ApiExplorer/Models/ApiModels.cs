namespace Kaya.ApiExplorer.Models;

public class ApiController
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ApiEndpoint> Endpoints { get; set; } = [];
    public bool RequiresAuthorization { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool IsObsolete { get; set; }
    public string? ObsoleteMessage { get; set; }
}

public class ApiEndpoint
{
    public string MethodName { get; set; } = string.Empty;
    public string HttpMethodType { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ApiParameter> Parameters { get; set; } = [];
    public ApiRequestBody? RequestBody { get; set; }
    public ApiResponse? Response { get; set; }
    public List<ApiProducesResponse> ProducesResponses { get; set; } = [];
    public bool RequiresAuthorization { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool IsObsolete { get; set; }
    public string? ObsoleteMessage { get; set; }
}

public class ApiParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // Query, Route, Body, Header, File, Form
    public bool Required { get; set; }
    public string Description { get; set; } = string.Empty;
    public object? DefaultValue { get; set; }
    public ApiSchema? Schema { get; set; } // For complex types
    public bool IsFile { get; set; } // Indicates if this is a file upload parameter
    public bool IsEnum { get; set; }
    public string? HeaderName { get; set; } // The actual header name when using [FromHeader(Name = "...")]
    public ApiConstraints? Constraints { get; set; }
}

public class ApiConstraints
{
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string? Pattern { get; set; }
    public string? Format { get; set; } // "email", "url", "phone", "credit-card"
}

public class ApiRequestBody
{
    public string Type { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ApiSchema? Schema { get; set; }
}

public class ApiResponse
{
    public string Type { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ApiSchema? Schema { get; set; }
}

public class ApiProducesResponse
{
    public int StatusCode { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public ApiSchema? Schema { get; set; }
}

public class ApiSchema
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, ApiProperty> Properties { get; set; } = [];
    public List<string> Required { get; set; } = [];
    public string Example { get; set; } = string.Empty;
}

public class ApiProperty
{
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public ApiSchema? NestedSchema { get; set; } // For nested complex types
    public ApiConstraints? Constraints { get; set; }
}

public class ApiDocumentation
{
    public string Title { get; set; } = "API Documentation";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public ApiDocumentationContact? Contact { get; set; }
    public ApiDocumentationLicense? License { get; set; }
    public string? TermsOfService { get; set; }
    public List<ApiDocumentationServer> Servers { get; set; } = [];
    public List<ApiController> Controllers { get; set; } = [];
}

public class ApiDocumentationContact
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Url { get; set; }
}

public class ApiDocumentationLicense
{
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
}

public class ApiDocumentationServer
{
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
}
