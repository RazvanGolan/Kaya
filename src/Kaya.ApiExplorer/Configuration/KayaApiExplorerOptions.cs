using System.Diagnostics.CodeAnalysis;

namespace Kaya.ApiExplorer.Configuration;

public class KayaApiExplorerOptions
{
    public MiddlewareOptions Middleware { get; set; } = new();
    public SignalRDebugOptions SignalRDebug { get; set; } = new();
    public DocumentationOptions Documentation { get; set; } = new();
}

public class MiddlewareOptions
{
    public string RoutePrefix { get; set; } = "/kaya";
    public string DefaultTheme { get; set; } = "light";
}

[ExcludeFromCodeCoverage(Justification = "Plain configuration DTO — no logic to test.")]
public class SignalRDebugOptions
{
    public bool Enabled { get; set; }
    public string RoutePrefix { get; set; } = "/kaya-signalr";
}

/// <summary>
/// Configures the metadata shown in the API documentation and exported OpenAPI spec.
/// </summary>
public class DocumentationOptions
{
    /// <summary>Title of the API. Displayed in the UI header and in the OpenAPI info object.</summary>
    public string Title { get; set; } = "API Documentation";

    /// <summary>API version string (e.g. "1.0.0", "v2").</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Short description of the API shown beneath the title.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional contact information included in the OpenAPI spec.</summary>
    public ContactOptions? Contact { get; set; }

    /// <summary>Optional license information included in the OpenAPI spec.</summary>
    public LicenseOptions? License { get; set; }

    /// <summary>URL to the terms of service for the API.</summary>
    public string? TermsOfService { get; set; }

    /// <summary>
    /// Server base URLs added to the OpenAPI spec's servers array.
    /// If empty, no servers object is emitted and tooling defaults to the current host.
    /// </summary>
    public List<ServerOptions> Servers { get; set; } = [];
}

[ExcludeFromCodeCoverage(Justification = "Plain configuration DTO — no logic to test.")]
public class ContactOptions
{
    /// <summary>Name of the contact person or organisation.</summary>
    public string? Name { get; set; }

    /// <summary>Email address of the contact.</summary>
    public string? Email { get; set; }

    /// <summary>URL pointing to contact information.</summary>
    public string? Url { get; set; }
}

[ExcludeFromCodeCoverage(Justification = "Plain configuration DTO — no logic to test.")]
public class LicenseOptions
{
    /// <summary>Name of the license (e.g. "MIT", "Apache 2.0").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL to the license text.</summary>
    public string? Url { get; set; }
}

[ExcludeFromCodeCoverage(Justification = "Plain configuration DTO — no logic to test.")]
public class ServerOptions
{
    /// <summary>Base URL of the server (e.g. "https://api.example.com/v1").</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional human-readable description of this server.</summary>
    public string? Description { get; set; }
}
