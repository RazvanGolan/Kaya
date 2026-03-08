using System.Diagnostics.CodeAnalysis;
using Kaya.GrpcExplorer.Configuration;
using Kaya.GrpcExplorer.Middleware;
using Kaya.GrpcExplorer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.GrpcExplorer.Extensions;

/// <summary>
/// Extension methods for setting up Kaya gRPC Explorer in an ASP.NET Core application
/// </summary>
[ExcludeFromCodeCoverage(Justification = "DI service registration and ASP.NET Core pipeline configuration; requires a full application host to exercise and is verified through integration tests.")]
public static class ServiceExtensions
{
    /// <summary>
    /// Adds Kaya gRPC Explorer services to the dependency injection container.
    /// Also registers gRPC reflection services required for service discovery.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Action to configure options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddKayaGrpcExplorer(
        this IServiceCollection services,
        Action<KayaGrpcExplorerOptions>? configure = null)
    {
        var options = new KayaGrpcExplorerOptions();
        configure?.Invoke(options);

        // Register options
        services.AddSingleton(options);

        // Register gRPC reflection (required for Kaya service discovery)
        services.AddGrpcReflection();

        // Register services
        services.AddSingleton<IGrpcServiceScanner, GrpcServiceScanner>();
        services.AddSingleton<IStreamingSessionManager, StreamingSessionManager>();
        services.AddSingleton<IGrpcProxyService, GrpcProxyService>();
        services.AddSingleton<IGrpcUiService, GrpcUiService>();

        return services;
    }

    /// <summary>
    /// Adds Kaya gRPC Explorer middleware to the application pipeline
    /// and maps the gRPC reflection service endpoint.
    /// </summary>
    /// <param name="app">The application builder (must be an IEndpointRouteBuilder, e.g. WebApplication)</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseKayaGrpcExplorer(this IApplicationBuilder app)
    {
        // Map gRPC reflection endpoint if the app supports endpoint routing
        if (app is IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapGrpcReflectionService();
        }

        return app.UseMiddleware<GrpcExplorerMiddleware>();
    }
}
