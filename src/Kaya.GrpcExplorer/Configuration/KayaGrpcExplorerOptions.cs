namespace Kaya.GrpcExplorer.Configuration;

/// <summary>
/// Configuration options for Kaya gRPC Explorer middleware
/// </summary>
public class KayaGrpcExplorerOptions
{
    /// <summary>
    /// Middleware configuration
    /// </summary>
    public MiddlewareOptions Middleware { get; init; } = new();
}

/// <summary>
/// Options for the gRPC Explorer middleware
/// </summary>
public class MiddlewareOptions
{
    /// <summary>
    /// Route prefix for the gRPC Explorer UI (default: "/grpc-explorer").
    /// </summary>
    public string RoutePrefix { get; set; } = "/grpc-explorer";

    /// <summary>
    /// Default server address pre-populated in the UI when no value is stored in the browser
    /// (default: "localhost:5001").
    /// </summary>
    public string DefaultServerAddress { get; set; } = "localhost:5001";

    /// <summary>
    /// Enable insecure connections (http instead of https) - for development only.
    /// </summary>
    public bool AllowInsecureConnections { get; set; } = false;

    /// <summary>
    /// Regex patterns matched (case-insensitive) against each discovered gRPC method's
    /// fully-qualified path in the form "package.Service/Method".
    /// Methods matching any pattern are skipped during scanning.
    /// Example: ["^grpc\\.reflection\\.", "/Internal[A-Z]"].
    /// </summary>
    public List<string> ExcludePathPatterns { get; set; } = [];
}
