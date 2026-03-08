using Kaya.ApiExplorer.Configuration;
using Kaya.ApiExplorer.Middleware;
using Kaya.ApiExplorer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.ApiExplorer.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kaya API Explorer in middleware mode with the specified route prefix and theme
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="routePrefix">The route prefix (default: "/kaya")</param>
    /// <param name="defaultTheme">The default theme ("light" or "dark")</param>
    /// <returns></returns>
    public static IServiceCollection AddKayaApiExplorer(this IServiceCollection services, string routePrefix = "/kaya", string defaultTheme = "light")
    {
        return services.AddKayaApiExplorer(options =>
        {
            options.Middleware.RoutePrefix = routePrefix;
            options.Middleware.DefaultTheme = defaultTheme;
        });
    }
    
    /// <summary>
    /// Adds Kaya API Explorer with full configuration options including SignalR debugging
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Configuration action for all options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddKayaApiExplorer(this IServiceCollection services, Action<KayaApiExplorerOptions> configureOptions)
    {
        services.AddSingleton<IEndpointScanner, EndpointScanner>();
        services.AddSingleton<IUIService, UIService>();
        services.AddSingleton<IOpenApiExportService, OpenApiExportService>();
        
        var options = new KayaApiExplorerOptions();
        configureOptions(options);

        services.AddSingleton(options);
        
        // Register SignalR debugging services if enabled
        if (options.SignalRDebug.Enabled)
        {
            services.AddSingleton<ISignalRHubScanner, SignalRHubScanner>();
            services.AddSingleton<ISignalRUIService, SignalRUIService>();
        }
        
        return services;
    }
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseKayaApiExplorer(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<KayaApiExplorerOptions>();
        var routePrefix = options?.Middleware.RoutePrefix ?? "/kaya";
        var result = app.UseMiddleware<ApiExplorerMiddleware>(routePrefix);
        
        if (options?.SignalRDebug.Enabled is true)
        {
            result = result.UseMiddleware<SignalRDebugMiddleware>(options.SignalRDebug.RoutePrefix);
        }
        
        return result;
    }
}
